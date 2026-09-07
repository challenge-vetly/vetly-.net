using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.ListaEspera;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Lista de espera por horário (RN-004/RN-037).
///
/// Fecha a terceira saída da RN-004: sem horário disponível, o Responsável entra na
/// fila daquele veterinário em vez de a demanda se perder.
/// </summary>
public class ListaEsperaService : IListaEsperaService
{
    private readonly IListaEsperaRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IAgendaRepository _agendaRepo;
    private readonly IConsultaService _consultaService;
    private readonly INotificacaoService _notificacoes;
    private readonly IUsuarioAtual _usuario;

    /// <summary>
    /// Janela de prioridade sobre a vaga oferecida, repetida no aviso (RN-037).
    ///
    /// Vem de <see cref="ItemListaEspera.JanelaDePrioridade"/> e não de um número
    /// solto: o texto que o Responsável lê tem de dizer o mesmo prazo que o domínio
    /// vai cobrar — prometer trinta minutos e expirar em quinze é pior que não avisar.
    /// </summary>
    private static int MinutosDePrioridade => (int)ItemListaEspera.JanelaDePrioridade.TotalMinutes;

    public ListaEsperaService(
        IListaEsperaRepository repo,
        IAnimalRepository animalRepo,
        IVeterinarioRepository vetRepo,
        IAgendaRepository agendaRepo,
        IConsultaService consultaService,
        INotificacaoService notificacoes,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _vetRepo = vetRepo;
        _agendaRepo = agendaRepo;
        _consultaService = consultaService;
        _notificacoes = notificacoes;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<ItemListaEsperaDto> EntrarAsync(EntrarNaListaDto dto)
    {
        var animal = await _animalRepo.ObterPorIdAsync(dto.AnimalId)
            ?? throw new NotFoundException("Animal", dto.AnimalId);

        GarantirPosseDoAnimal(animal);

        _ = await _vetRepo.ObterPorIdAsync(dto.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", dto.VeterinarioId);

        // Entrar duas vezes na mesma fila so atrapalharia a ordem de quem esperou
        var jaNaFila = await _repo.ObterAguardandoDoAnimalAsync(dto.AnimalId, dto.VeterinarioId);
        if (jaNaFila is not null)
            throw new ConflitoDeEstadoException("RN-004", "Este animal ja esta na lista de espera deste veterinario.");

        var item = new ItemListaEspera(animal.TutorId, animal.Id, dto.VeterinarioId, dto.Necessidade);

        await _repo.AdicionarAsync(item);
        await _repo.SalvarAsync();

        return await MapearAsync(item);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ItemListaEsperaDto>> ObterDoTutorAsync(Guid tutorId)
    {
        if (!_usuario.EhAdmin && _usuario.TutorId != tutorId)
            throw new AcessoNegadoException("RN-105", "Esta lista nao pertence ao seu escopo de acesso.");

        var itens = await _repo.ObterDoTutorAsync(tutorId);

        var resultado = new List<ItemListaEsperaDto>();
        foreach (var item in itens)
            resultado.Add(await MapearAsync(item));

        return resultado;
    }

    /// <inheritdoc/>
    public async Task SairAsync(Guid id)
    {
        var item = await ObterComPosseAsync(id);

        item.Cancelar();
        _repo.Atualizar(item);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<CheckoutCriadoDto> ConfirmarVagaAsync(Guid id, Guid servicoId)
    {
        var item = await ObterComPosseAsync(id);
        var agora = DateTime.UtcNow;

        if (item.Estado != EstadoListaEspera.Notificado)
            throw new BusinessRuleException("RN-037", "Nao ha vaga oferecida para confirmar.");

        if (!item.PrioridadeValida(agora))
        {
            // A janela venceu: encerra este pedido e passa a vaga adiante, senao a fila
            // fica presa em quem nao respondeu (RN-037)
            item.Expirar();
            _repo.Atualizar(item);
            await _repo.SalvarAsync();

            throw new ConflitoDeEstadoException("RN-037",
                "A prioridade sobre esta vaga expirou. Entre na fila novamente ou escolha outro horario.");
        }

        var slotId = item.SlotOferecidoId!.Value;

        // A confirmacao entra no mesmo checkout do fluxo normal (§4.1): mesma trava de
        // horario, mesma politica de reembolso, mesmo caminho de pagamento.
        var checkout = await _consultaService.IniciarCheckoutAsync(new CheckoutDto
        {
            AnimalId = item.AnimalId,
            PrestadorId = item.VeterinarioId,
            SlotId = slotId,
            ServicoId = servicoId
        });

        item.Confirmar();
        _repo.Atualizar(item);
        await _repo.SalvarAsync();

        return checkout;
    }

    /// <inheritdoc/>
    public async Task<ItemListaEsperaDto?> PromoverProximoAsync(Guid slotId)
    {
        var slot = await _agendaRepo.ObterSlotAsync(slotId);

        // So se promove sobre horario realmente disponivel
        if (slot is null || !slot.EstaDisponivel(DateTime.UtcNow))
            return null;

        var agora = DateTime.UtcNow;

        // Expira quem foi notificado e nao respondeu, para nao travar a fila
        var vencidos = await _repo.ObterNotificadosVencidosAsync(slot.VeterinarioId, agora);
        foreach (var vencido in vencidos)
        {
            vencido.Expirar();
            _repo.Atualizar(vencido);
        }

        var proximo = await _repo.ObterPrimeiroAguardandoAsync(slot.VeterinarioId);

        if (proximo is null)
        {
            if (vencidos.Any()) await _repo.SalvarAsync();
            return null;
        }

        proximo.Notificar(slot.Id, agora);
        _repo.Atualizar(proximo);
        await _repo.SalvarAsync();

        // §3.4/§6.2: a vaga aberta e um dos eventos que a matriz de canais exige
        // avisar. Sem isto o estado do pedido dizia "Notificado" e ninguem tinha sido
        // notificado — a prioridade de 15 minutos corria contra alguem que nao sabia
        // que ela tinha comecado, e a fila expirava sozinha ate o fim.
        await AvisarVagaAbertaAsync(proximo, slot);

        return await MapearAsync(proximo);
    }

    /// <summary>
    /// Avisa o primeiro da fila de que a vaga abriu (RN-037/RN-092).
    ///
    /// O aviso carrega o horário e o prazo porque a resposta é em um toque: quem lê
    /// "abriu vaga" sem saber quando é nem até quando pode responder tem de abrir o
    /// app para descobrir as duas coisas, e a janela é de quinze minutos.
    ///
    /// Falha de notificação não desfaz a oferta. O pedido já está gravado como
    /// <c>Notificado</c>, e derrubar aqui deixaria a vaga sem dono enquanto o próximo
    /// da fila continuaria esperando — o pior dos dois desfechos.
    /// </summary>
    private async Task AvisarVagaAbertaAsync(ItemListaEspera item, Slot slot)
    {
        var vet = await _vetRepo.ObterPorIdAsync(item.VeterinarioId);
        var comQuem = vet is null ? "seu veterinario" : vet.Nome;

        await _notificacoes.CriarAsync(new CriarNotificacaoDto
        {
            TutorId = item.TutorId,
            Tipo = TipoNotificacao.HorarioDisponivel,
            Titulo = "Abriu vaga na sua lista de espera",
            Corpo = $"Abriu um horario com {comQuem} em {slot.Inicio:dd/MM 'as' HH:mm} (UTC). " +
                    $"Confirme em ate {MinutosDePrioridade} minutos para garantir a vaga.",
            AnimalId = item.AnimalId,
            Destino = $"/lista-espera/{item.Id}"
        });
    }

    private async Task<ItemListaEspera> ObterComPosseAsync(Guid id)
    {
        var item = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Item da lista de espera", id);

        if (!_usuario.EhAdmin && _usuario.TutorId != item.TutorId)
            throw new AcessoNegadoException("RN-105", "Este pedido nao pertence ao seu escopo de acesso.");

        return item;
    }

    private void GarantirPosseDoAnimal(Animal animal)
    {
        if (_usuario.EhAdmin || _usuario.TutorId == animal.TutorId)
            return;

        throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");
    }

    private async Task<ItemListaEsperaDto> MapearAsync(ItemListaEspera item)
    {
        var slot = item.SlotOferecidoId is { } slotId ? await _agendaRepo.ObterSlotAsync(slotId) : null;

        return new ItemListaEsperaDto
        {
            Id = item.Id,
            TutorId = item.TutorId,
            AnimalId = item.AnimalId,
            VeterinarioId = item.VeterinarioId,
            Necessidade = item.Necessidade,
            Estado = item.Estado,
            CriadoEm = item.CriadoEm,
            SlotOferecidoId = item.SlotOferecidoId,
            HorarioOferecido = slot?.Inicio,
            PrioridadeAte = item.PrioridadeAte
        };
    }
}
