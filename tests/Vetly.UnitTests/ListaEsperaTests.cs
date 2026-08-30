using Moq;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.ListaEspera;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Lista de espera por horario (RN-004/RN-037): entrada na fila, oferta da vaga com
/// prioridade de 15 minutos, confirmacao e expiracao.
/// </summary>
public class ListaEsperaTests
{
    private readonly Mock<IListaEsperaRepository> _repo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IAgendaRepository> _agendaRepo = new();
    private readonly Mock<IConsultaService> _consultaService = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Animal _animal;
    private readonly Veterinario _vet;

    public ListaEsperaTests()
    {
        _animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());
        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(_animal);
        _vetRepo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<ItemListaEspera>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.Atualizar(It.IsAny<ItemListaEspera>()));
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repo.Setup(r => r.ObterAguardandoDoAnimalAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((ItemListaEspera?)null);
        _repo.Setup(r => r.ObterNotificadosVencidosAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);
    }

    private ListaEsperaService CriarServico() =>
        new(_repo.Object, _animalRepo.Object, _vetRepo.Object,
            _agendaRepo.Object, _consultaService.Object, _usuario.Object);

    private EntrarNaListaDto Entrada() => new()
    {
        AnimalId = _animal.Id,
        VeterinarioId = _vet.Id,
        Necessidade = TipoServico.ConsultaRotina
    };

    private Slot SlotLivre()
    {
        var slot = new Slot(_vet.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddMinutes(30));
        _agendaRepo.Setup(r => r.ObterSlotAsync(slot.Id)).ReturnsAsync(slot);
        return slot;
    }

    // ── Entrada na fila (RN-004) ─────────────────────────────────────────────

    [Fact]
    public async Task Entrar_ColocaOResponsavelNaFilaAguardando()
    {
        var item = await CriarServico().EntrarAsync(Entrada());

        Assert.Equal(EstadoListaEspera.Aguardando, item.Estado);
        Assert.Equal(_animal.TutorId, item.TutorId);
        Assert.Null(item.SlotOferecidoId);
    }

    [Fact]
    public async Task Entrar_DuasVezesNaMesmaFila_Retorna409()
    {
        var jaNaFila = new ItemListaEspera(_animal.TutorId, _animal.Id, _vet.Id, TipoServico.ConsultaRotina);
        _repo.Setup(r => r.ObterAguardandoDoAnimalAsync(_animal.Id, _vet.Id)).ReturnsAsync(jaNaFila);

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().EntrarAsync(Entrada()));

        // Entrar duas vezes so atrapalharia a ordem de quem esperou
        Assert.Equal("RN-004", ex.Codigo);
    }

    [Fact]
    public async Task Entrar_ComAnimalDeOutroResponsavel_ERecusado()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);
        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().EntrarAsync(Entrada()));
    }

    // ── Oferta da vaga (RN-037) ──────────────────────────────────────────────

    [Fact]
    public async Task Promover_OfereceAVagaAoPrimeiroDaFilaComQuinzeMinutos()
    {
        var slot = SlotLivre();
        var primeiro = new ItemListaEspera(_animal.TutorId, _animal.Id, _vet.Id, TipoServico.ConsultaRotina);
        _repo.Setup(r => r.ObterPrimeiroAguardandoAsync(_vet.Id)).ReturnsAsync(primeiro);

        var antes = DateTime.UtcNow;
        var promovido = await CriarServico().PromoverProximoAsync(slot.Id);

        Assert.NotNull(promovido);
        Assert.Equal(EstadoListaEspera.Notificado, promovido!.Estado);
        Assert.Equal(slot.Id, promovido.SlotOferecidoId);
        Assert.InRange(promovido.PrioridadeAte!.Value, antes.AddMinutes(14), antes.AddMinutes(16));
    }

    [Fact]
    public async Task Promover_FilaVazia_NaoFazNada()
    {
        var slot = SlotLivre();
        _repo.Setup(r => r.ObterPrimeiroAguardandoAsync(_vet.Id)).ReturnsAsync((ItemListaEspera?)null);

        Assert.Null(await CriarServico().PromoverProximoAsync(slot.Id));
    }

    [Fact]
    public async Task Promover_HorarioJaOcupado_NaoOferece()
    {
        var slot = SlotLivre();
        slot.TravarParaCheckout(Guid.NewGuid(), DateTime.UtcNow);
        slot.Confirmar();

        Assert.Null(await CriarServico().PromoverProximoAsync(slot.Id));
        _repo.Verify(r => r.ObterPrimeiroAguardandoAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Promover_ExpiraQuemNaoRespondeuAntesDeOferecerAoProximo()
    {
        var slot = SlotLivre();

        var naoRespondeu = new ItemListaEspera(Guid.NewGuid(), Guid.NewGuid(), _vet.Id, TipoServico.ConsultaRotina);
        naoRespondeu.Notificar(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-20));

        var proximo = new ItemListaEspera(_animal.TutorId, _animal.Id, _vet.Id, TipoServico.ConsultaRotina);

        _repo.Setup(r => r.ObterNotificadosVencidosAsync(_vet.Id, It.IsAny<DateTime>()))
            .ReturnsAsync([naoRespondeu]);
        _repo.Setup(r => r.ObterPrimeiroAguardandoAsync(_vet.Id)).ReturnsAsync(proximo);

        await CriarServico().PromoverProximoAsync(slot.Id);

        // A fila nao pode ficar presa em quem nao respondeu (RN-037)
        Assert.Equal(EstadoListaEspera.Expirado, naoRespondeu.Estado);
        Assert.Equal(EstadoListaEspera.Notificado, proximo.Estado);
    }

    // ── Confirmação da vaga (RN-037) ─────────────────────────────────────────

    [Fact]
    public async Task ConfirmarVaga_DentroDaPrioridade_SegueParaOCheckout()
    {
        var slot = SlotLivre();
        var item = new ItemListaEspera(_animal.TutorId, _animal.Id, _vet.Id, TipoServico.ConsultaRotina);
        item.Notificar(slot.Id, DateTime.UtcNow);
        _repo.Setup(r => r.ObterPorIdAsync(item.Id)).ReturnsAsync(item);

        var servicoId = Guid.NewGuid();
        _consultaService
            .Setup(s => s.IniciarCheckoutAsync(It.Is<CheckoutDto>(c => c.SlotId == slot.Id)))
            .ReturnsAsync(new CheckoutCriadoDto { ConsultaId = Guid.NewGuid(), Status = StatusConsulta.EmCheckout });

        var checkout = await CriarServico().ConfirmarVagaAsync(item.Id, servicoId);

        // A confirmacao entra no mesmo checkout do fluxo normal: mesma trava, mesma
        // politica de reembolso, mesmo caminho de pagamento (§4.1)
        Assert.Equal(StatusConsulta.EmCheckout, checkout.Status);
        Assert.Equal(EstadoListaEspera.Confirmado, item.Estado);
    }

    [Fact]
    public async Task ConfirmarVaga_PrioridadeVencida_Retorna409EEncerraOPedido()
    {
        var item = new ItemListaEspera(_animal.TutorId, _animal.Id, _vet.Id, TipoServico.ConsultaRotina);
        item.Notificar(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-20));
        _repo.Setup(r => r.ObterPorIdAsync(item.Id)).ReturnsAsync(item);

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().ConfirmarVagaAsync(item.Id, Guid.NewGuid()));

        Assert.Equal("RN-037", ex.Codigo);
        Assert.Equal(EstadoListaEspera.Expirado, item.Estado);
        _consultaService.Verify(s => s.IniciarCheckoutAsync(It.IsAny<CheckoutDto>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmarVaga_SemVagaOferecida_NaoProssegue()
    {
        var item = new ItemListaEspera(_animal.TutorId, _animal.Id, _vet.Id, TipoServico.ConsultaRotina);
        _repo.Setup(r => r.ObterPorIdAsync(item.Id)).ReturnsAsync(item);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ConfirmarVagaAsync(item.Id, Guid.NewGuid()));

        Assert.Equal("RN-037", ex.Codigo);
    }

    [Fact]
    public async Task Sair_CancelaOPedido()
    {
        var item = new ItemListaEspera(_animal.TutorId, _animal.Id, _vet.Id, TipoServico.ConsultaRotina);
        _repo.Setup(r => r.ObterPorIdAsync(item.Id)).ReturnsAsync(item);

        await CriarServico().SairAsync(item.Id);

        Assert.Equal(EstadoListaEspera.Cancelado, item.Estado);
    }

    [Fact]
    public async Task Sair_DePedidoDeOutroResponsavel_ERecusado()
    {
        var deOutro = new ItemListaEspera(Guid.NewGuid(), Guid.NewGuid(), _vet.Id, TipoServico.ConsultaRotina);
        _repo.Setup(r => r.ObterPorIdAsync(deOutro.Id)).ReturnsAsync(deOutro);
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().SairAsync(deOutro.Id));
    }

    // ── Invariantes do domínio ───────────────────────────────────────────────

    [Fact]
    public void Notificar_QuemNaoEstaAguardando_NaoEPermitido()
    {
        var item = new ItemListaEspera(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TipoServico.Banho);
        item.Cancelar();

        Assert.Throws<InvalidOperationException>(() => item.Notificar(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Expirar_SoltaAVagaOferecida()
    {
        var item = new ItemListaEspera(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TipoServico.Banho);
        item.Notificar(Guid.NewGuid(), DateTime.UtcNow);

        item.Expirar();

        Assert.Equal(EstadoListaEspera.Expirado, item.Estado);
        Assert.Null(item.SlotOferecidoId);
        Assert.Null(item.PrioridadeAte);
    }
}
