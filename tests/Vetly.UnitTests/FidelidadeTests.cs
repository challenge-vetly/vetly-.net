using Moq;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Programa de fidelidade (RN-046 a RN-054).
///
/// Os parametros sao fechados no vetly-tech §1: 1 ponto por real, 50 pontos por
/// obrigacao cumprida, 100 pontos = R$ 3,00, tiers com multiplicador, FIFO em 12
/// meses, cupom de 30 dias e financiamento por faixa.
/// </summary>
public class FidelidadeTests
{
    private readonly Mock<IFidelidadeRepository> _repo = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly List<MovimentoDePontos> _extrato = [];
    private readonly List<CupomResgate> _cupons = [];

    public FidelidadeTests()
    {
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);

        _repo.Setup(r => r.AdicionarAsync(It.IsAny<MovimentoDePontos>()))
            .Callback<MovimentoDePontos>(_extrato.Add).Returns(Task.CompletedTask);

        _repo.Setup(r => r.AdicionarCupomAsync(It.IsAny<CupomResgate>()))
            .Callback<CupomResgate>(_cupons.Add).Returns(Task.CompletedTask);

        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repo.Setup(r => r.ObterDoTutorAsync(It.IsAny<Guid>())).ReturnsAsync(() => _extrato);
        _repo.Setup(r => r.ObterLotesComSaldoAsync(It.IsAny<Guid>()))
            .ReturnsAsync(() => _extrato.Where(m => m.Tipo == TipoMovimentoDePontos.Credito && m.Restante > 0));
        _repo.Setup(r => r.ObterCuponsDoTutorAsync(It.IsAny<Guid>())).ReturnsAsync(() => _cupons);
        _repo.Setup(r => r.ObterCuponsVencidosAsync(It.IsAny<DateTime>())).ReturnsAsync([]);
        _repo.Setup(r => r.ObterCreditoDaConsultaAsync(It.IsAny<Guid>())).ReturnsAsync((MovimentoDePontos?)null);
        _repo.Setup(r => r.ObterEstornoDaConsultaAsync(It.IsAny<Guid>())).ReturnsAsync((MovimentoDePontos?)null);
        _repo.Setup(r => r.ObterCreditoDaObrigacaoAsync(It.IsAny<Guid>())).ReturnsAsync((MovimentoDePontos?)null);
        _repo.Setup(r => r.ObterCreditosVencidosSemBaixaAsync(It.IsAny<DateTime>())).ReturnsAsync([]);
    }

    private FidelidadeService CriarServico() =>
        new(_repo.Object, _consultaRepo.Object, _pagamentoRepo.Object, _usuario.Object);

    /// <summary>Coloca um crédito direto no extrato, sem passar pelo serviço.</summary>
    private MovimentoDePontos Credito(decimal valorPago, TierFidelidade tier = TierFidelidade.Bronze)
    {
        var m = MovimentoDePontos.PorServicoPago(_tutorId, Guid.NewGuid(), valorPago, tier);
        _extrato.Add(m);

        return m;
    }

    private Consulta AtendimentoPago(decimal valor = 200m, bool confirmado = true, bool realizada = true)
    {
        var consulta = Consulta.ParaCheckout(
            DateTime.UtcNow.AddDays(-1), Guid.NewGuid(), Guid.NewGuid(), _tutorId,
            Guid.NewGuid(), Guid.NewGuid());

        consulta.ConfirmarPagamento();

        if (realizada)
            consulta.Finalizar();

        var pagamento = new Pagamento(_tutorId, valor, MeioPagamento.Pix, consulta.Id);

        if (confirmado)
            pagamento.Confirmar();

        _consultaRepo.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _pagamentoRepo.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(pagamento);

        return consulta;
    }

    private static SimularResgateDto Resgate(int pontos) => new()
    {
        ItemRef = "mock-racao-premium-15kg",
        ItemNome = "Ração Premium 15kg",
        Categoria = CategoriaItem.Alimentacao,
        Pontos = pontos
    };

    // ── Conversão e ganho (RN-047/RN-049) ────────────────────────────────────

    [Fact]
    public void Conversao_CemPontosValemTresReais()
    {
        // RN-049: calibrado sobre o retorno de ~3% do gasto praticado no mercado
        Assert.Equal(3.00m, RegrasDeFidelidade.EmReais(100));
        Assert.Equal(30.00m, RegrasDeFidelidade.EmReais(1000));
        Assert.Equal(0.03m, RegrasDeFidelidade.EmReais(1));
    }

    [Fact]
    public void Conversao_PontosNecessarios_ArredondaParaCima()
    {
        // Nao se da desconto que os pontos nao cobrem
        Assert.Equal(334, RegrasDeFidelidade.PontosPara(10.00m));
        Assert.Equal(1000, RegrasDeFidelidade.PontosPara(30.00m));
    }

    [Fact]
    public async Task Credito_PorServicoPago_DaUmPontoPorReal()
    {
        var consulta = AtendimentoPago(valor: 180m);

        var movimento = await CriarServico().CreditarPorConsultaAsync(consulta.Id);

        // RN-047: 1 ponto por R$ 1, arredondado para baixo
        Assert.Equal(180, movimento!.Pontos);
        Assert.Equal(180, movimento.PontosBrutos);
    }

    [Fact]
    public async Task Credito_PorObrigacaoCumprida_DaCinquentaPontosFixos()
    {
        var movimento = await CriarServico().CreditarPorObrigacaoAsync(
            _tutorId, Guid.NewGuid(), "Antirrabica");

        // RN-047: recompensa comportamento de cuidado, nao gasto — vale o mesmo numa
        // consulta de R$ 80 e numa de R$ 300
        Assert.Equal(50, movimento!.Pontos);
        Assert.Contains("Antirrabica", movimento.Descricao);
    }

    [Fact]
    public async Task Credito_DaMesmaObrigacao_NaoAconteceDuasVezes()
    {
        var obrigacaoId = Guid.NewGuid();
        _repo.Setup(r => r.ObterCreditoDaObrigacaoAsync(obrigacaoId))
            .ReturnsAsync(MovimentoDePontos.PorObrigacaoCumprida(_tutorId, obrigacaoId, "V10", TierFidelidade.Bronze));

        Assert.Null(await CriarServico().CreditarPorObrigacaoAsync(_tutorId, obrigacaoId, "V10"));
    }

    [Fact]
    public async Task Credito_ConsultaNaoRealizada_NaoGeraPonto()
    {
        var consulta = AtendimentoPago(realizada: false);

        Assert.Null(await CriarServico().CreditarPorConsultaAsync(consulta.Id));
    }

    [Fact]
    public async Task Credito_PagamentoNaoConfirmado_NaoGeraPonto()
    {
        var consulta = AtendimentoPago(confirmado: false);

        // RN-052: o programa pagaria por receita que nao entrou
        Assert.Null(await CriarServico().CreditarPorConsultaAsync(consulta.Id));
    }

    // ── Tier e multiplicador (RN-048) ────────────────────────────────────────

    [Theory]
    [InlineData(0, TierFidelidade.Bronze)]
    [InlineData(999, TierFidelidade.Bronze)]
    [InlineData(1000, TierFidelidade.Prata)]
    [InlineData(2999, TierFidelidade.Prata)]
    [InlineData(3000, TierFidelidade.Ouro)]
    public void Tier_SegueAsFaixasFechadas(int acumulo, TierFidelidade esperado)
    {
        Assert.Equal(esperado, RegrasDeFidelidade.TierPara(acumulo));
    }

    [Theory]
    [InlineData(TierFidelidade.Bronze, 1.0)]
    [InlineData(TierFidelidade.Prata, 1.25)]
    [InlineData(TierFidelidade.Ouro, 1.5)]
    public void Multiplicador_CresceNoTopo(TierFidelidade tier, double esperado)
    {
        Assert.Equal((decimal)esperado, RegrasDeFidelidade.MultiplicadorDe(tier));
    }

    [Fact]
    public async Task Credito_AplicaOMultiplicadorDoTierVigente()
    {
        // Acumulo de 1.200 pontos coloca o Responsavel em Prata
        Credito(1200m);

        var consulta = AtendimentoPago(valor: 200m);

        var movimento = await CriarServico().CreditarPorConsultaAsync(consulta.Id);

        // 200 brutos × 1,25 = 250 creditados
        Assert.Equal(200, movimento!.PontosBrutos);
        Assert.Equal(1.25m, movimento.Multiplicador);
        Assert.Equal(250, movimento.Pontos);
    }

    [Fact]
    public async Task Tier_ContaOQueFoiCreditado_NaoOSaldo()
    {
        Credito(1500m);
        await CriarServico().ResgatarAsync(_tutorId, Resgate(1400));

        var saldo = await CriarServico().ObterSaldoAsync(_tutorId);

        // Quem resgatou nao perde a faixa por ter usado o programa — usar e
        // exatamente o comportamento que o programa quer
        Assert.Equal(TierFidelidade.Prata, saldo.Tier);
        Assert.Equal(100, saldo.Saldo);
    }

    // ── Consumo FIFO e expiração (RN-050) ────────────────────────────────────

    [Fact]
    public async Task Resgate_ConsomeOLoteMaisAntigoPrimeiro()
    {
        var antigo = Credito(500m);
        var novo = Credito(500m);

        // Envelhece o primeiro lote para que o FIFO tenha o que ordenar
        typeof(MovimentoDePontos).GetProperty(nameof(MovimentoDePontos.ExpiraEm))!
            .SetValue(antigo, DateTime.UtcNow.AddDays(10));

        await CriarServico().ResgatarAsync(_tutorId, Resgate(300));

        // O ponto mais velho e o que esta mais perto de vencer: consumir o novo faria
        // o Responsavel perder o que ele acabou de usar para pagar
        Assert.Equal(200, antigo.Restante);
        Assert.Equal(500, novo.Restante);
    }

    [Fact]
    public async Task Resgate_AtravessaLotesQuandoUmSoNaoBasta()
    {
        var primeiro = Credito(100m);
        var segundo = Credito(100m);

        typeof(MovimentoDePontos).GetProperty(nameof(MovimentoDePontos.ExpiraEm))!
            .SetValue(primeiro, DateTime.UtcNow.AddDays(5));

        await CriarServico().ResgatarAsync(_tutorId, Resgate(150));

        Assert.Equal(0, primeiro.Restante);
        Assert.Equal(50, segundo.Restante);
    }

    [Fact]
    public async Task Expiracao_BaixaSomenteOQueSobrouDoLote()
    {
        var lote = Credito(300m);
        await CriarServico().ResgatarAsync(_tutorId, Resgate(250));

        typeof(MovimentoDePontos).GetProperty(nameof(MovimentoDePontos.ExpiraEm))!
            .SetValue(lote, DateTime.UtcNow.AddDays(-1));

        _repo.Setup(r => r.ObterCreditosVencidosSemBaixaAsync(It.IsAny<DateTime>())).ReturnsAsync([lote]);

        var expirados = await CriarServico().ExpirarVencidosAsync();

        // O que ja foi gasto saiu no debito: cobrar de novo deixaria o saldo negativo
        Assert.Equal(50, expirados);
        Assert.Equal(0, lote.Restante);
    }

    [Fact]
    public void Validade_DoCredito_EDeDozeMeses()
    {
        var credito = Credito(100m);

        Assert.True(credito.ExpiraEm > DateTime.UtcNow.AddDays(360));
        Assert.True(credito.ExpiraEm < DateTime.UtcNow.AddDays(370));
    }

    // ── Financiamento do desconto (RN-051) ───────────────────────────────────

    [Fact]
    public void Faixa_AteDezReais_EBancadaSoPelaVetly()
    {
        var (vetly, prestador, faixa) = RegrasDeFidelidade.Dividir(10.00m);

        // Desconto pequeno nao onera o vet, o que preserva a adesao ao programa
        Assert.Equal(FaixaDeFinanciamento.Ate10, faixa);
        Assert.Equal(10.00m, vetly);
        Assert.Equal(0m, prestador);
    }

    [Fact]
    public void Faixa_DeDezATrinta_EDivididaSessentaQuarenta()
    {
        var (vetly, prestador, faixa) = RegrasDeFidelidade.Dividir(20.00m);

        Assert.Equal(FaixaDeFinanciamento.De10a30, faixa);
        Assert.Equal(12.00m, vetly);
        Assert.Equal(8.00m, prestador);
    }

    [Fact]
    public void Faixa_AcimaDeTrinta_EDivididaTrintaSetenta()
    {
        var (vetly, prestador, faixa) = RegrasDeFidelidade.Dividir(50.00m);

        // Resgate grande e co-financiado por quem captura a recorrencia
        Assert.Equal(FaixaDeFinanciamento.Acima30, faixa);
        Assert.Equal(15.00m, vetly);
        Assert.Equal(35.00m, prestador);
    }

    [Fact]
    public void Faixa_AsDuasPartesSempreFechamODesconto()
    {
        foreach (var desconto in new[] { 0.03m, 9.99m, 10.01m, 17.77m, 30.01m, 123.45m })
        {
            var (vetly, prestador, _) = RegrasDeFidelidade.Dividir(desconto);

            // Arredondar as duas separadamente deixaria centavos sem dono
            Assert.Equal(desconto, vetly + prestador);
        }
    }

    // ── Simulação e resgate (RN-017/RN-018/RN-053) ───────────────────────────

    [Fact]
    public async Task Simulacao_MostraODescontoEADivisaoSemGravarNada()
    {
        Credito(1000m);

        var simulacao = await CriarServico().SimularResgateAsync(_tutorId, Resgate(700));

        Assert.Equal(21.00m, simulacao.Desconto);
        Assert.Equal(FaixaDeFinanciamento.De10a30, simulacao.Faixa);
        Assert.Equal(12.60m, simulacao.ValorVetly);
        Assert.Equal(8.40m, simulacao.ValorPrestador);
        Assert.Equal(300, simulacao.SaldoApos);

        // No MVP a divisao e calculada e exibida, sem movimentacao real
        Assert.Equal("Simulado", simulacao.Abatimento);
        Assert.Empty(_cupons);
    }

    [Fact]
    public async Task Simulacao_SemSaldo_NaoEAceita()
    {
        Credito(100m);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().SimularResgateAsync(_tutorId, Resgate(500)));

        Assert.Equal("RN-050", ex.Codigo);
    }

    [Fact]
    public async Task Resgate_EmiteCupomComTrintaDiasDeValidade()
    {
        Credito(1000m);

        var cupom = await CriarServico().ResgatarAsync(_tutorId, Resgate(700));

        Assert.Equal(StatusCupom.Emitido, cupom.Status);
        Assert.Equal(30, Math.Round((cupom.ExpiraEm - cupom.EmitidoEm).TotalDays));
        Assert.StartsWith("VETLY-", cupom.CodigoQr);
        Assert.Equal(CategoriaItem.Alimentacao, cupom.Categoria);
    }

    [Fact]
    public async Task Resgate_GravaADivisaoDaIncidenciaNoCupom()
    {
        Credito(1000m);

        var cupom = await CriarServico().ResgatarAsync(_tutorId, Resgate(700));

        Assert.Equal(21.00m, cupom.Desconto);
        Assert.Equal(12.60m, cupom.DescontoVetly);
        Assert.Equal(8.40m, cupom.DescontoPrestador);
    }

    [Fact]
    public async Task Resgate_DebitaOSaldoDeVerdade()
    {
        Credito(1000m);

        await CriarServico().ResgatarAsync(_tutorId, Resgate(700));

        var saldo = await CriarServico().ObterSaldoAsync(_tutorId);

        Assert.Equal(300, saldo.Saldo);
    }

    [Fact]
    public void Cupom_Vencido_NaoDevolvePontos()
    {
        var cupom = new CupomResgate(
            _tutorId, "mock-item", "Item", CategoriaItem.Higiene, 700, 21.00m);

        cupom.Expirar();

        // RN-053: o nao retorno evita passivo perpetuo e resgate especulativo
        Assert.Equal(StatusCupom.Expirado, cupom.Status);
        Assert.Equal(700, cupom.PontosDebitados);
    }

    [Fact]
    public void Cupom_JaResgatado_NaoResgataDeNovo()
    {
        var cupom = new CupomResgate(
            _tutorId, "mock-item", "Item", CategoriaItem.Higiene, 700, 21.00m);

        cupom.Resgatar(DateTime.UtcNow);

        // RN-054: um cupom vale para um item e uma transacao
        Assert.Throws<InvalidOperationException>(() => cupom.Resgatar(DateTime.UtcNow));
    }

    // ── Estorno (RN-052) ─────────────────────────────────────────────────────

    [Fact]
    public async Task Estorno_DesfazOCreditoDaConsultaCancelada()
    {
        var consulta = AtendimentoPago(valor: 200m);
        var credito = await CriarServico().CreditarPorConsultaAsync(consulta.Id);

        _repo.Setup(r => r.ObterCreditoDaConsultaAsync(consulta.Id))
            .ReturnsAsync(_extrato.First(m => m.Id == credito!.Id));

        var estornados = await CriarServico().EstornarPorConsultaAsync(consulta.Id);

        Assert.Equal(200, estornados);

        var saldo = await CriarServico().ObterSaldoAsync(_tutorId);
        Assert.Equal(0, saldo.Saldo);
    }

    [Fact]
    public async Task Estorno_NaoCobraDeVoltaPontoJaGasto()
    {
        var consulta = AtendimentoPago(valor: 500m);
        var credito = await CriarServico().CreditarPorConsultaAsync(consulta.Id);
        var lote = _extrato.First(m => m.Id == credito!.Id);

        _repo.Setup(r => r.ObterCreditoDaConsultaAsync(consulta.Id)).ReturnsAsync(lote);

        await CriarServico().ResgatarAsync(_tutorId, Resgate(400));

        var estornados = await CriarServico().EstornarPorConsultaAsync(consulta.Id);

        // Deixar o saldo negativo por algo que o Responsavel usou de boa-fe antes do
        // cancelamento seria cobrar duas vezes pelo mesmo ponto
        Assert.Equal(100, estornados);

        var saldo = await CriarServico().ObterSaldoAsync(_tutorId);
        Assert.Equal(0, saldo.Saldo);
    }

    [Fact]
    public async Task Estorno_DuasVezes_NaoDebitaDuasVezes()
    {
        var consulta = AtendimentoPago();
        var credito = await CriarServico().CreditarPorConsultaAsync(consulta.Id);

        _repo.Setup(r => r.ObterCreditoDaConsultaAsync(consulta.Id))
            .ReturnsAsync(_extrato.First(m => m.Id == credito!.Id));

        await CriarServico().EstornarPorConsultaAsync(consulta.Id);

        _repo.Setup(r => r.ObterEstornoDaConsultaAsync(consulta.Id))
            .ReturnsAsync(_extrato.First(m => m.Tipo == TipoMovimentoDePontos.Estorno));

        Assert.Equal(0, await CriarServico().EstornarPorConsultaAsync(consulta.Id));
    }

    [Fact]
    public async Task Estorno_SemCredito_NaoFazNada()
    {
        Assert.Equal(0, await CriarServico().EstornarPorConsultaAsync(Guid.NewGuid()));
    }

    // ── Escopo (RN-106) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Saldo_DeOutroResponsavel_ERecusado()
    {
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterSaldoAsync(Guid.NewGuid()));

        Assert.Equal("RN-106", ex.Codigo);
    }
}
