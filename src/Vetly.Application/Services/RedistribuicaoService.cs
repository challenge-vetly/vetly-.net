using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.DTOs.Redistribuicao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Redistribuição de consultas quando o profissional sai ou fica indisponível
/// (RN-025).
///
/// Cancelar em massa jogaria o problema no colo do Responsável, que agendou de
/// boa-fé e teria de refazer tudo — inclusive pagar de novo. Redistribuir preserva o
/// pagamento, o animal e o compromisso; o que muda é quem atende.
/// </summary>
public class RedistribuicaoService : IRedistribuicaoService
{
    private readonly IConsultaRepository _consultaRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IAgendaRepository _agendaRepo;
    private readonly INotificacaoService _notificacoes;
    private readonly IUsuarioAtual _usuario;

    /// <summary>
    /// Janela em que se procura horário para o candidato: três dias para cada lado do
    /// original. Fora disso já não é remanejamento, é outro agendamento.
    /// </summary>
    private static readonly TimeSpan JanelaDeBusca = TimeSpan.FromDays(3);

    /// <summary>Quantos candidatos a lista devolve. Mais que isso não ajuda a decidir.</summary>
    private const int MaximoDeCandidatos = 5;

    public RedistribuicaoService(
        IConsultaRepository consultaRepo,
        IVeterinarioRepository vetRepo,
        IAnimalRepository animalRepo,
        IAgendaRepository agendaRepo,
        INotificacaoService notificacoes,
        IUsuarioAtual usuario)
    {
        _consultaRepo = consultaRepo;
        _vetRepo = vetRepo;
        _animalRepo = animalRepo;
        _agendaRepo = agendaRepo;
        _notificacoes = notificacoes;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CandidatoARedistribuicaoDto>> SugerirCandidatosAsync(Guid consultaId)
    {
        GarantirAdmin();

        var consulta = await ObterRedistribuivelAsync(consultaId);

        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId)
            ?? throw new NotFoundException("Animal", consulta.AnimalId);

        var atual = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId);

        // Mesma UF do profissional que sai: consulta presencial não se remaneja para
        // outro estado, e o Responsável agendou perto de onde mora (RN-026).
        var naRegiao = await _vetRepo.ObterPorUfAsync(atual?.UfAtuacao ?? string.Empty);

        var candidatos = new List<CandidatoARedistribuicaoDto>();

        var de = consulta.DataHora.Subtract(JanelaDeBusca);
        var ate = consulta.DataHora.Add(JanelaDeBusca);
        var agora = DateTime.UtcNow;

        foreach (var vet in naRegiao)
        {
            if (vet.Id == consulta.VeterinarioId || !vet.Ativo || !vet.Publicado)
                continue;

            // RN-029: espécie é eliminatória. Encaminhar um felino para quem só atende
            // cães não é uma sugestão pior, é uma sugestão errada.
            if (!AtendeEspecie(vet, animal.Especie))
                continue;

            var slots = await _agendaRepo.ObterSlotsAsync(vet.Id, de, ate);

            var maisProximo = slots
                .Where(s => s.EstaDisponivel(agora) && s.Inicio > agora)
                .OrderBy(s => Math.Abs((s.Inicio - consulta.DataHora).TotalMinutes))
                .FirstOrDefault();

            if (maisProximo is null)
                continue;

            candidatos.Add(new CandidatoARedistribuicaoDto
            {
                VeterinarioId = vet.Id,
                Nome = vet.Nome,
                Crmv = vet.Crmv.Valor,
                EmpresaId = vet.EmpresaId,
                SlotId = maisProximo.Id,
                NovoHorario = maisProximo.Inicio,
                DiferencaEmHoras = Math.Round((maisProximo.Inicio - consulta.DataHora).TotalHours, 1),
                AtendeEspecie = true,
                NotaMedia = vet.NotaMedia,
                NotaPublica = vet.TemNotaPublica()
            });
        }

        // Proximidade do horário original, e não reputação: quem agendou às 14h de
        // terça organizou o dia em torno disso. Trocar o profissional já é uma quebra.
        return candidatos
            .OrderBy(c => Math.Abs(c.DiferencaEmHoras))
            .Take(MaximoDeCandidatos);
    }

    /// <inheritdoc/>
    public async Task<RedistribuicaoRealizadaDto> RedistribuirAsync(Guid consultaId, RedistribuirConsultaDto dto)
    {
        GarantirAdmin();

        var consulta = await ObterRedistribuivelAsync(consultaId);

        var novoVet = await _vetRepo.ObterPorIdAsync(dto.NovoVeterinarioId)
            ?? throw new NotFoundException("Veterinario", dto.NovoVeterinarioId);

        if (!novoVet.Ativo)
            throw new BusinessRuleException("RN-025",
                "Nao e possivel redistribuir para um veterinario desativado.");

        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId)
            ?? throw new NotFoundException("Animal", consulta.AnimalId);

        if (!AtendeEspecie(novoVet, animal.Especie))
            throw new BusinessRuleException("RN-029",
                $"O veterinario escolhido nao atende a especie {animal.Especie}.");

        var novoSlot = await _agendaRepo.ObterSlotAsync(dto.NovoSlotId)
            ?? throw new NotFoundException("Slot", dto.NovoSlotId);

        if (novoSlot.VeterinarioId != novoVet.Id)
            throw new ValidationException("novoSlotId",
                "O horario escolhido nao pertence ao veterinario informado.");

        // O horário é travado antes de mover a consulta: sem isso, duas
        // redistribuições simultâneas mandariam dois animais para o mesmo slot.
        if (!novoSlot.TravarParaCheckout(consulta.Id, DateTime.UtcNow))
            throw new ConflitoDeEstadoException("RN-035",
                "O horario escolhido nao esta mais disponivel.");

        var vetAnterior = consulta.VeterinarioId;
        var horarioAnterior = consulta.DataHora;
        var slotAnterior = consulta.SlotId;

        consulta.Redistribuir(novoVet.Id, novoSlot.Id, novoSlot.Inicio);

        // Consulta paga volta a ser confirmada no novo horário: o pagamento não se
        // desfaz porque o profissional mudou.
        if (consulta.StatusPagamento == StatusPagamento.Confirmado)
            novoSlot.Confirmar();

        _agendaRepo.AtualizarSlot(novoSlot);

        // O horário antigo volta à disponibilidade — é vaga que alguém pode usar
        await LiberarSlotAnteriorAsync(slotAnterior);

        _consultaRepo.Atualizar(consulta);

        await _agendaRepo.SalvarAsync();
        await _consultaRepo.SalvarAsync();

        var notificado = await AvisarResponsavelAsync(consulta, novoVet, horarioAnterior, dto.Motivo);

        return new RedistribuicaoRealizadaDto
        {
            ConsultaId = consulta.Id,
            VeterinarioAnteriorId = vetAnterior,
            NovoVeterinarioId = novoVet.Id,
            NovoVeterinarioNome = novoVet.Nome,
            HorarioAnterior = horarioAnterior,
            NovoHorario = consulta.DataHora,
            Motivo = dto.Motivo,
            ResponsavelNotificado = notificado,
            RealizadaEm = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Avisa o Responsável (RN-092). Redistribuir sem avisar seria trocar o
    /// profissional de alguém sem contar — a notificação é parte da operação, não um
    /// extra.
    /// </summary>
    private async Task<bool> AvisarResponsavelAsync(
        Consulta consulta, Veterinario novoVet, DateTime horarioAnterior, string motivo)
    {
        var mudouOHorario = consulta.DataHora != horarioAnterior;

        var corpo = mudouOHorario
            ? $"Sua consulta passou para {novoVet.Nome} em {consulta.DataHora:dd/MM 'as' HH:mm} (UTC). {motivo}"
            : $"Sua consulta passou para {novoVet.Nome}, no mesmo horario. {motivo}";

        await _notificacoes.CriarAsync(new CriarNotificacaoDto
        {
            TutorId = consulta.TutorId,
            Tipo = TipoNotificacao.ConsultaConfirmada,
            Titulo = "Mudanca no seu atendimento",
            Corpo = corpo,
            AnimalId = consulta.AnimalId,
            ConsultaId = consulta.Id,
            Destino = $"/consultas/{consulta.Id}"
        });

        return true;
    }

    /// <summary>Devolve o horário antigo à disponibilidade — é vaga que alguém pode usar.</summary>
    private async Task LiberarSlotAnteriorAsync(Guid? slotId)
    {
        if (slotId is not { } id)
            return;

        var slot = await _agendaRepo.ObterSlotAsync(id);

        if (slot is null)
            return;

        slot.Liberar();
        _agendaRepo.AtualizarSlot(slot);
    }

    /// <summary>Só consulta que ainda vai acontecer se redistribui (RN-025).</summary>
    private async Task<Consulta> ObterRedistribuivelAsync(Guid consultaId)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (consulta.Status is not (StatusConsulta.Confirmada or StatusConsulta.EmCheckout))
            throw new BusinessRuleException("RN-025",
                $"Consulta com status {consulta.Status} nao pode ser redistribuida.");

        return consulta;
    }

    /// <summary>
    /// A espécie é eliminatória (RN-029). Vet sem espécie declarada atende todas — é
    /// o cadastro antigo, e recusar todos eles paralisaria a redistribuição.
    /// </summary>
    private static bool AtendeEspecie(Veterinario vet, string especie) =>
        vet.EspeciesAtendidas.Count == 0
        || vet.EspeciesAtendidas.Any(e => string.Equals(e, especie, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Redistribuir é ato de operação: muda o profissional de um atendimento que outra
    /// pessoa contratou. Nem o veterinário que sai decide para quem vai (RN-106).
    /// </summary>
    private void GarantirAdmin()
    {
        if (!_usuario.EhAdmin)
            throw new AcessoNegadoException("RN-106",
                "A redistribuicao de consultas e restrita a administracao.");
    }
}
