using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Painel do veterinario (RN-105).
///
/// Nao e relatorio: e o que precisa da atencao dele agora.
/// </summary>
public class DashboardVeterinarioTests
{
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IDocumentoRepository> _documentoRepo = new();
    private readonly Mock<ICapturaRepository> _capturaRepo = new();
    private readonly Mock<IAvaliacaoRepository> _avaliacaoRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Veterinario _vet;

    public DashboardVeterinarioTests()
    {
        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);
        _vetRepo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);

        _consultaRepo.Setup(r => r.ObterPorVeterinarioAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync([]);

        _documentoRepo.Setup(r => r.ObterPorConsultaAsync(It.IsAny<Guid>())).ReturnsAsync([]);
        _avaliacaoRepo.Setup(r => r.ObterDoVeterinarioAsync(It.IsAny<Guid>())).ReturnsAsync([]);
        _capturaRepo.Setup(r => r.ObterSessaoDaConsultaAsync(It.IsAny<Guid>())).ReturnsAsync((SessaoCaptura?)null);
        _capturaRepo.Setup(r => r.ObterRascunhoDaConsultaAsync(It.IsAny<Guid>())).ReturnsAsync((RascunhoIa?)null);
        _pagamentoRepo.Setup(r => r.ObterPorConsultaAsync(It.IsAny<Guid>())).ReturnsAsync((Pagamento?)null);
    }

    private DashboardService CriarServico() =>
        new(_consultaRepo.Object, _vetRepo.Object, _animalRepo.Object, _pagamentoRepo.Object,
            _documentoRepo.Object, _capturaRepo.Object, _avaliacaoRepo.Object, _usuario.Object);

    private Animal Animal(string nome = "Thor", decimal? peso = 28m)
    {
        var animal = new Animal(nome, "Canino", "SRD", new DateTime(2022, 3, 1), Guid.NewGuid());

        if (peso is { } kg)
            animal.RegistrarPeso(kg);

        _animalRepo.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);

        return animal;
    }

    private Consulta Consulta(Animal animal, int horaDoDia = 10, bool cancelada = false)
    {
        var quando = DateTime.UtcNow.Date.AddHours(horaDoDia);

        var consulta = Domain.Entities.Consulta.ParaCheckout(
            quando, _vet.Id, animal.Id, animal.TutorId, Guid.NewGuid(), Guid.NewGuid());

        consulta.ConfirmarPagamento();

        if (cancelada)
            consulta.Cancelar();

        return consulta;
    }

    private void Agenda(params Consulta[] consultas) =>
        _consultaRepo.Setup(r => r.ObterPorVeterinarioAsync(
            _vet.Id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(consultas);

    private Pagamento Pagamento(Consulta consulta, decimal valor = 200m, decimal repasse = 176m,
        bool liquidado = false, bool confirmado = true)
    {
        var pagamento = new Pagamento(consulta.TutorId, valor, MeioPagamento.Pix, consulta.Id);

        if (confirmado)
            pagamento.Confirmar();

        pagamento.RegistrarSplit(PlanoAssinatura.Profissional, 12m, valor - repasse, repasse, _vet.Id);

        if (liquidado)
            pagamento.Liquidar();

        _pagamentoRepo.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(pagamento);

        return pagamento;
    }

    // ── Agenda do dia ────────────────────────────────────────────────────────

    [Fact]
    public async Task Agenda_TrazOsAtendimentosNaOrdemDoDia()
    {
        var animal = Animal();
        Agenda(Consulta(animal, 16), Consulta(animal, 9), Consulta(animal, 13));

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.Equal(3, painel.AgendaDeHoje.Count);
        Assert.True(painel.AgendaDeHoje[0].DataHora < painel.AgendaDeHoje[1].DataHora);
    }

    [Fact]
    public async Task Agenda_OmiteConsultaCancelada()
    {
        var animal = Animal();
        Agenda(Consulta(animal, 9), Consulta(animal, 11, cancelada: true));

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        // O painel serve para conduzir o dia, e horario cancelado nao e atendimento
        Assert.Single(painel.AgendaDeHoje);
    }

    [Fact]
    public async Task Agenda_MarcaOAnimalSemPesoCadastrado()
    {
        var semPeso = Animal("Rex", peso: null);
        Agenda(Consulta(semPeso));

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        // Descobrir que falta peso durante a consulta e tarde (RN-081)
        Assert.True(painel.AgendaDeHoje[0].PesoAusente);
    }

    [Fact]
    public async Task Agenda_ComAnimalComPeso_NaoAcendeOAviso()
    {
        Agenda(Consulta(Animal()));

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.False(painel.AgendaDeHoje[0].PesoAusente);
    }

    // ── Pendências ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Pendencias_ContaConsultaIniciadaENuncaEncerrada()
    {
        var consulta = Consulta(Animal());
        Agenda(consulta);

        var sessaoAberta = new SessaoCaptura(consulta.Id, capturaAtiva: true);
        _capturaRepo.Setup(r => r.ObterSessaoDaConsultaAsync(consulta.Id)).ReturnsAsync(sessaoAberta);

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        // A consulta nao gera nada enquanto nao encerra (RN-008)
        Assert.Equal(1, painel.Pendencias.ConsultasNaoEncerradas);
        Assert.True(painel.Pendencias.TemPendencia);
    }

    [Fact]
    public async Task Pendencias_ContaRascunhoSemDecisao()
    {
        var consulta = Consulta(Animal());
        Agenda(consulta);

        _capturaRepo.Setup(r => r.ObterRascunhoDaConsultaAsync(consulta.Id)).ReturnsAsync(
            new RascunhoIa(Guid.NewGuid(), consulta.Id, "a", "b", ["c"], "d", "e", "origem", "modelo", false, [], 10));

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        // Rascunho sem decisao nao gera documento (RN-082)
        Assert.Equal(1, painel.Pendencias.RascunhosAguardandoDecisao);
    }

    [Fact]
    public async Task Pendencias_ContaDocumentoQueExigeAssinaturaESemEla()
    {
        var consulta = Consulta(Animal());
        Agenda(consulta);

        var receita = new Documento(TipoDocumento.ReceitaVeterinaria, "12345-SP", consulta.Id);
        var prontuario = new Documento(TipoDocumento.Prontuario, "12345-SP", consulta.Id);

        _documentoRepo.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync([receita, prontuario]);

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        // So a receita trava: prontuario nao exige assinatura (C-04)
        Assert.Equal(1, painel.Pendencias.DocumentosAguardandoAssinatura);
    }

    [Fact]
    public async Task Pendencias_ContaAvaliacaoSemResposta()
    {
        _avaliacaoRepo.Setup(r => r.ObterDoVeterinarioAsync(_vet.Id)).ReturnsAsync(
        [
            new Avaliacao(Guid.NewGuid(), Guid.NewGuid(), _vet.Id, 4),
            new Avaliacao(Guid.NewGuid(), Guid.NewGuid(), _vet.Id, 5)
        ]);

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.Equal(2, painel.Pendencias.AvaliacoesSemResposta);
    }

    [Fact]
    public async Task Pendencias_AvaliacaoSemResposta_NaoAcendeOAvisoDeTrava()
    {
        _avaliacaoRepo.Setup(r => r.ObterDoVeterinarioAsync(_vet.Id)).ReturnsAsync(
            [new Avaliacao(Guid.NewGuid(), Guid.NewGuid(), _vet.Id, 4)]);

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        // TemPendencia e sobre o que trava dinheiro ou documento; responder avaliacao
        // e desejavel, nao bloqueante
        Assert.False(painel.Pendencias.TemPendencia);
    }

    [Fact]
    public async Task Pendencias_SemNada_NaoAcendeAviso()
    {
        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.False(painel.Pendencias.TemPendencia);
    }

    // ── Números do mês ───────────────────────────────────────────────────────

    [Fact]
    public async Task Mes_SomaSomenteOQueFoiCobrado()
    {
        var animal = Animal();
        var paga = Consulta(animal, 9);
        var naoPaga = Consulta(animal, 11);

        Agenda(paga, naoPaga);
        Pagamento(paga);
        Pagamento(naoPaga, confirmado: false);

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.Equal(1, painel.Mes.AtendimentosRealizados);
        Assert.Equal(200m, painel.Mes.ValorBruto);
        Assert.Equal(176m, painel.Mes.RepasseApurado);
    }

    [Fact]
    public async Task Mes_SeparaORepassePendenteDoLiquidado()
    {
        var animal = Animal();
        var liquidada = Consulta(animal, 9);
        var pendente = Consulta(animal, 11);

        Agenda(liquidada, pendente);
        Pagamento(liquidada, liquidado: true);
        Pagamento(pendente);

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.Equal(352m, painel.Mes.RepasseApurado);
        Assert.Equal(176m, painel.Mes.RepassePendente);
    }

    [Fact]
    public async Task Mes_ContaCancelamentosSemSomarDinheiro()
    {
        var animal = Animal();
        Agenda(Consulta(animal, 9, cancelada: true));

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.Equal(1, painel.Mes.Cancelamentos);
        Assert.Equal(0m, painel.Mes.ValorBruto);
    }

    // ── Escopo (RN-105) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Painel_SemTokenDeVeterinario_ERecusado()
    {
        _usuario.SetupGet(u => u.VeterinarioId).Returns((Guid?)null);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterDoVeterinarioAsync(null));

        Assert.Equal("RN-105", ex.Codigo);
    }

    [Fact]
    public async Task Painel_TrazAReputacaoAtual()
    {
        _vet.AtualizarReputacao(4.5m, 8);

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.Equal(4.5m, painel.NotaMedia);
        Assert.True(painel.NotaPublica);
    }

    [Fact]
    public async Task Painel_ComPoucasAvaliacoes_NaoMostraNotaComoPublica()
    {
        _vet.AtualizarReputacao(5m, 2);

        var painel = await CriarServico().ObterDoVeterinarioAsync(null);

        Assert.False(painel.NotaPublica);
    }
}
