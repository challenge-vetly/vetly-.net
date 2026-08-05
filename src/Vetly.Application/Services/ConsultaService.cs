using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Exame;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Cancelamento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de consultas. Orquestra a máquina de estados do agendamento (RN-056..061),
/// pré-sintomas (RN-059), consentimento LGPD (RN-041/084) e cancelamento via Strategy
/// (RN-019/020/021).
/// </summary>
public class ConsultaService : IConsultaService
{
    // Procedimentos físicos exigem modalidade presencial — reafirma RN-025/057.
    private static readonly HashSet<TipoServico> ServicosFisicos =
        [TipoServico.Vacinacao, TipoServico.Cirurgia, TipoServico.Exame];

    private readonly IConsultaRepository _repo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IDocumentoRepository _documentoRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IConsentimentoLgpdRepository _consentimentoRepo;
    private readonly IResponsavelRepository _responsavelRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IAcessoProntuarioService _acessoProntuarioService;
    private readonly IAvaliacaoService _avaliacaoService;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly IEnumerable<ICancelamentoStrategy> _strategies;

    public ConsultaService(
        IConsultaRepository repo,
        IPagamentoRepository pagamentoRepo,
        IDocumentoRepository documentoRepo,
        IAnimalRepository animalRepo,
        IConsentimentoLgpdRepository consentimentoRepo,
        IResponsavelRepository responsavelRepo,
        IVeterinarioRepository vetRepo,
        IAcessoProntuarioService acessoProntuarioService,
        IAvaliacaoService avaliacaoService,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        IEnumerable<ICancelamentoStrategy> strategies)
    {
        _repo = repo;
        _pagamentoRepo = pagamentoRepo;
        _documentoRepo = documentoRepo;
        _animalRepo = animalRepo;
        _consentimentoRepo = consentimentoRepo;
        _responsavelRepo = responsavelRepo;
        _vetRepo = vetRepo;
        _acessoProntuarioService = acessoProntuarioService;
        _avaliacaoService = avaliacaoService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _strategies = strategies;
    }

    public async Task<IEnumerable<ConsultaDto>> ObterTodosAsync(
        DateTime? dataInicio, DateTime? dataFim, Guid? veterinarioId, StatusConsulta? status)
    {
        var consultas = await _repo.ObterComFiltrosAsync(dataInicio, dataFim, veterinarioId, status);
        return consultas.Select(MapearParaDto);
    }

    public async Task<ConsultaDto> ObterPorIdAsync(Guid id)
    {
        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);
        return MapearParaDto(consulta);
    }

    public async Task<IEnumerable<ConsultaDto>> ObterPorVeterinarioAsync(Guid veterinarioId)
    {
        var consultas = await _repo.ObterPorVeterinarioAsync(veterinarioId);
        return consultas.Select(MapearParaDto);
    }

    public async Task<IEnumerable<ConsultaDto>> ObterPorAnimalAsync(Guid animalId)
    {
        var consultas = await _repo.ObterPorAnimalAsync(animalId);
        return consultas.Select(MapearParaDto);
    }

    /// <summary>
    /// Agenda uma consulta: exige consentimento de atendimento clínico ativo (LGPD-001),
    /// valida que serviços físicos sejam presenciais (RN-057) e cria a consulta já em
    /// EmCheckout com lock de 10 min (RN-058). O pagamento é confirmado depois, em uma
    /// etapa separada (RN-037).
    /// </summary>
    public async Task<ConsultaDto> AgendarAsync(CriarConsultaDto dto)
    {
        var temConsentimentoClinico = await _consentimentoRepo.ObterAtivoAsync(
            dto.ResponsavelId, FinalidadeConsentimento.AtendimentoClinico) is not null;
        if (!temConsentimentoClinico)
            throw new BusinessRuleException("LGPD-001",
                "O responsavel precisa ter o consentimento de atendimento clinico ativo para agendar consultas.");

        if (ServicosFisicos.Contains(dto.TipoServico) && dto.Modalidade != ModalidadeAtendimento.Presencial)
            throw new BusinessRuleException("RN-057",
                $"O servico '{dto.TipoServico}' exige modalidade presencial.");

        var consulta = new Consulta(
            dto.DataHora, dto.Modalidade, dto.TipoServico, dto.VeterinarioId,
            dto.AnimalId, dto.ResponsavelId, dto.PreSintomas);
        consulta.IniciarCheckout(_timeProvider.GetUtcNow().UtcDateTime);

        await _repo.AdicionarAsync(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>
    /// Confirma o pagamento da consulta, transicionando EmCheckout → Confirmada (RN-058).
    /// Se o Responsável tem consentimento de compartilhamento na rede ativo, concede acesso
    /// de colmeia ao vet da consulta (RN-083).
    /// </summary>
    public async Task<ConsultaDto> ConfirmarPagamentoAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        consulta.ConfirmarPagamento(agora);
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        await _acessoProntuarioService.ConcederAcessoPorConsultaAsync(
            consulta.Id, consulta.VeterinarioId, consulta.AnimalId, consulta.ResponsavelId, consulta.DataHora, agora);

        return MapearParaDto(consulta);
    }

    /// <summary>
    /// Cancela a consulta aplicando a Strategy de reembolso de menor prioridade aplicavel (RN-019/020/021).
    /// A transição de estado em si (só permitida a partir de EmCheckout/Confirmada) é validada
    /// pelo próprio domínio — CONSULTA-010 em qualquer outra origem.
    /// </summary>
    public async Task<ResultadoCancelamentoDto> CancelarAsync(Guid id)
    {
        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);

        // Valida a transição de estado antes de qualquer trabalho com pagamento/strategy —
        // CONSULTA-010 imediato se a consulta já estiver num estado que não permite cancelar.
        consulta.Cancelar();

        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(id)
            ?? throw new BusinessRuleException("CONSULTA-002", "Pagamento da consulta nao encontrado.");

        // Seleciona a strategy de menor prioridade que seja aplicavel ao momento do cancelamento
        var agora = DateTime.UtcNow;
        var strategy = _strategies
            .OrderBy(s => s.Prioridade)
            .First(s => s.Aplicavel(consulta.DataHora, agora));

        var resultado = strategy.Executar(pagamento, percentualRetencao: 30m);
        resultado.Janela = CalcularJanela(consulta.DataHora, agora);
        resultado.Liquidado = false; // MVP: calculado e registrado, nunca liquidado (RN-037/062)

        pagamento.Estornar(resultado.ValorReembolso);

        _repo.Atualizar(consulta);
        _pagamentoRepo.Atualizar(pagamento);
        await _repo.SalvarAsync();

        // RN-081: se a consulta já tinha avaliação publicada, o cancelamento a invalida.
        // No estado atual da máquina de estados (Cancelar só parte de EmCheckout/Confirmada,
        // nunca de Realizada) isso é inalcançável na prática — mantido para não deixar a
        // regra sem cobertura caso um fluxo futuro permita cancelar após realizada.
        await _avaliacaoService.InvalidarPorCancelamentoAsync(consulta.Id, agora);

        return resultado;
    }

    /// <summary>
    /// Cancela a consulta por iniciativa do veterinário: crédito de cortesia de 10% do
    /// valor pago (teto R$ 30) + strike de reputação (RN-065/067). Diferente de
    /// <see cref="CancelarAsync"/>: não exige um pagamento vinculado — se não houver
    /// nenhum, apenas o strike é aplicado (nada a creditar).
    /// </summary>
    public async Task<CancelamentoPeloVeterinarioDto> CancelamentoPeloVeterinarioAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        consulta.Cancelar(); // valida a transição (CONSULTA-010) antes de seguir

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var (credito, suspenso) = await AplicarConsequenciasVeterinarioAsync(
            consulta, "Cancelamento pelo veterinário", agora);

        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        return new CancelamentoPeloVeterinarioDto
        {
            CreditoCortesia = credito, StrikeRegistrado = true, VeterinarioSuspenso = suspenso
        };
    }

    /// <summary>
    /// Aplica o crédito de cortesia (se houver pagamento vinculado) e o strike de
    /// reputação ao veterinário da consulta (RN-065/066/067). Compartilhado entre
    /// cancelamento pelo vet e no-show do vet, que têm a mesma consequência (RN-066).
    /// </summary>
    private async Task<(decimal credito, bool suspenso)> AplicarConsequenciasVeterinarioAsync(
        Consulta consulta, string motivoStrike, DateTime agora)
    {
        var credito = 0m;
        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(consulta.Id);
        if (pagamento is not null)
        {
            credito = Math.Min(pagamento.Valor * 0.10m, 30m);
            var responsavel = await _responsavelRepo.ObterPorIdAsync(consulta.ResponsavelId)
                ?? throw new NotFoundException("Responsavel", consulta.ResponsavelId);
            responsavel.CreditarSaldoCreditosVetly(credito);
            _responsavelRepo.Atualizar(responsavel);
        }

        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", consulta.VeterinarioId);
        vet.RegistrarStrike(agora, motivoStrike);
        _vetRepo.Atualizar(vet);

        return (credito, vet.EstaSuspenso(agora));
    }

    /// <summary>Classifica a janela de antecedência do cancelamento (RN-062/063).</summary>
    private static string CalcularJanela(DateTime horarioConsulta, DateTime horarioCancelamento)
    {
        var antecedencia = (horarioConsulta - horarioCancelamento).TotalHours;
        return antecedencia switch
        {
            > 24 => ">24h",
            > 2 and <= 24 => "24h-2h",
            _ => "<2h"
        };
    }

    /// <summary>
    /// Marca a consulta como realizada (RN-061): exige receita assinada digitalmente (RN-031)
    /// e que o chamador seja o veterinário responsável pela consulta.
    /// </summary>
    public async Task<ConsultaDto> MarcarRealizadaAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (_currentUser.EntidadeId is { } vetId && vetId != consulta.VeterinarioId)
            throw new ForbiddenException("ACESSO-002",
                "Somente o veterinario responsavel pode marcar esta consulta como realizada.");

        var receita = await _documentoRepo.ObterPorConsultaETipoAsync(consultaId, TipoDocumento.ReceitaVeterinaria);
        if (receita is null)
            throw new BusinessRuleException("RN-031", "Receita veterinaria nao encontrada para esta consulta.");
        if (!receita.AssinadoDigitalmente)
            throw new BusinessRuleException("RN-031", "A receita veterinaria deve estar assinada digitalmente.");

        consulta.MarcarRealizada(_timeProvider.GetUtcNow().UtcDateTime);
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>
    /// Registra no-show de uma das partes. No-show do responsável conta para o limiar de
    /// bloqueio de descontos (RN-064); no-show do veterinário recebe o mesmo tratamento do
    /// cancelamento pelo vet — crédito de cortesia + strike (RN-066).
    /// </summary>
    public async Task<ConsultaDto> RegistrarNoShowAsync(Guid consultaId, ParteNoShow parte)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        if (parte == ParteNoShow.Responsavel)
        {
            consulta.RegistrarNoShowResponsavel();

            var responsavel = await _responsavelRepo.ObterPorIdAsync(consulta.ResponsavelId)
                ?? throw new NotFoundException("Responsavel", consulta.ResponsavelId);
            responsavel.RegistrarNoShow(agora);
            _responsavelRepo.Atualizar(responsavel);
        }
        else
        {
            consulta.RegistrarNoShowVeterinario();
            await AplicarConsequenciasVeterinarioAsync(consulta, "No-show do veterinário", agora);
        }

        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>Remarca a consulta para uma nova data/hora, incrementando o contador de remarcações (RN-022).</summary>
    public async Task<ConsultaDto> RemarcarAsync(Guid consultaId, DateTime novaDataHora)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        consulta.Reagendar(novaDataHora);
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>
    /// Agrega dados pre-consulta: animal, pré-sintomas, historico (ultimas 5), exames recentes (ultimos 3).
    /// Para um veterinário, exige acesso ao animal (colmeia ou atendimento direto — RN-010/083)
    /// e registra o acesso no log de auditoria (RN-086).
    /// </summary>
    public async Task<BriefingConsultaDto> ObterBriefingAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId)
            ?? throw new NotFoundException("Animal", consulta.AnimalId);

        if (_currentUser.Role == "Veterinario")
        {
            var vetId = _currentUser.EntidadeId
                ?? throw new ForbiddenException("ACESSO-001", "Acesso ao prontuario negado.");
            var agora = _timeProvider.GetUtcNow().UtcDateTime;

            if (!await _acessoProntuarioService.PodeAcessarAsync(vetId, animal.Id, agora))
                throw new ForbiddenException("ACESSO-001", "Acesso ao prontuario negado.");

            await _acessoProntuarioService.RegistrarAcessoAsync(
                vetId, animal.Id, $"Briefing pré-consulta {consultaId}", agora);
        }

        var historico = (await _repo.ObterPorAnimalAsync(consulta.AnimalId))
            .OrderByDescending(c => c.DataHora)
            .Take(5)
            .Select(MapearParaDto)
            .ToList();

        var exames = (await _animalRepo.ObterExamesAsync(consulta.AnimalId))
            .OrderByDescending(e => e.DataSolicitacao)
            .Take(3)
            .Select(e => new ExameDto
            {
                Id = e.Id, AnimalId = e.AnimalId, VeterinarioId = e.VeterinarioId,
                TipoSolicitacao = e.TipoSolicitacao, Resultado = e.Resultado,
                LiberadoAoResponsavel = e.LiberadoAoResponsavel,
                DataSolicitacao = e.DataSolicitacao, DataResultado = e.DataResultado
            })
            .ToList();

        return new BriefingConsultaDto
        {
            ConsultaId = consultaId,
            Animal = AnimalService.MapearParaDto(animal),
            PreSintomas = consulta.PreSintomas,
            HistoricoResumido = historico,
            AlertasAtivos = animal.AlertasAtivos,
            ExamesRecentes = exames,
            UltimaConsulta = historico.Count > 0 ? historico[0].DataHora : null
        };
    }

    /// <summary>
    /// Registra a validacao manual do diagnostico pelo veterinario (RN-024).
    /// Pre-requisito para gerar documentos via DocumentoService.
    /// </summary>
    public async Task ValidarDiagnosticoAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (consulta.Status == StatusConsulta.Cancelada)
            throw new BusinessRuleException("CONSULTA-003",
                "Nao e possivel validar diagnostico de consulta cancelada.");

        consulta.ValidarDiagnostico();
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();
    }

    private static ConsultaDto MapearParaDto(Consulta c) => new()
    {
        Id = c.Id, DataHora = c.DataHora, Modalidade = c.Modalidade, TipoServico = c.TipoServico,
        VeterinarioId = c.VeterinarioId, AnimalId = c.AnimalId, ResponsavelId = c.ResponsavelId,
        PreSintomas = c.PreSintomas, Status = c.Status, LockCheckoutExpiraEm = c.LockCheckoutExpiraEm,
        ContadorRemarcacoes = c.ContadorRemarcacoes, DataRealizada = c.DataRealizada,
        DiagnosticoValidado = c.DiagnosticoValidado, ProtocoloValidado = c.ProtocoloValidado
    };
}
