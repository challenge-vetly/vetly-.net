using Moq;
using Vetly.Application.DTOs.Carteira;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Histórico unificado da carteira (RN-115): pagamentos, resgates e reembolsos numa
/// linha do tempo só, com filtros, ordenação, paginação e os totais do filtro.
/// </summary>
public class CarteiraHistoricoTests
{
    private readonly Mock<IPagamentoRepository> _pagamentos = new();
    private readonly Mock<IFidelidadeRepository> _fidelidade = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly List<Pagamento> _doTutor = [];
    private readonly List<CupomResgate> _cupons = [];

    private static readonly DateTime Hoje = DateTime.UtcNow.Date;

    public CarteiraHistoricoTests()
    {
        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);
        _pagamentos.Setup(r => r.ObterPorTutorAsync(_tutorId)).ReturnsAsync(() => _doTutor);
        _fidelidade.Setup(r => r.ObterCuponsDoTutorAsync(_tutorId)).ReturnsAsync(() => _cupons);
    }

    private CarteiraService CriarServico() => new(_pagamentos.Object, _fidelidade.Object, _usuario.Object);

    private Task<HistoricoDaCarteiraDto> Historico(
        FiltroHistoricoDaCarteiraDto? filtro = null, Paginacao? paginacao = null, Ordenacao? ordenacao = null) =>
        CriarServico().ObterHistoricoAsync(_tutorId, filtro ?? new(), paginacao ?? new(), ordenacao);

    /// <summary>
    /// Datas no passado sem setter publico: o dominio carimba UtcNow, e o historico so
    /// e testavel com momentos diferentes entre si.
    /// </summary>
    private static void Carimbar<T>(T entidade, string propriedade, DateTime quando) =>
        typeof(T).GetProperty(propriedade)!.SetValue(entidade, quando);

    private Pagamento Pago(decimal valor, int diasAtras, StatusPagamento status = StatusPagamento.Confirmado)
    {
        var pagamento = new Pagamento(_tutorId, valor, MeioPagamento.Pix, Guid.NewGuid());
        if (status != StatusPagamento.Pendente)
            pagamento.Confirmar();
        if (status == StatusPagamento.Recusado)
            pagamento.Recusar();

        Carimbar(pagamento, nameof(Pagamento.Momento), Hoje.AddDays(-diasAtras));
        _doTutor.Add(pagamento);
        return pagamento;
    }

    private CupomResgate Resgatado(int pontos, int diasAtras, Guid? itemId = null)
    {
        var cupom = new CupomResgate(_tutorId, "ref", "Banho e tosa", CategoriaItem.Higiene, pontos, pontos * 0.03m, itemId);
        Carimbar(cupom, nameof(CupomResgate.EmitidoEm), Hoje.AddDays(-diasAtras));
        _cupons.Add(cupom);
        return cupom;
    }

    // ── Unificação ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Historico_UnePagamentoResgateEReembolsoNoMesmoFormato()
    {
        // Arrange — um pagamento estornado vira duas linhas; o cupom, uma
        var pagamento = Pago(200m, diasAtras: 10);
        pagamento.Estornar(140m);
        Carimbar(pagamento, nameof(Pagamento.EstornadoEm), Hoje.AddDays(-5));
        var cupom = Resgatado(600, diasAtras: 2, itemId: Guid.NewGuid());

        // Act
        var historico = await Historico();

        // Assert — momento decrescente por padrao
        Assert.Equal(3, historico.Total);
        Assert.Equal(
            [TipoDeLancamento.Resgate, TipoDeLancamento.Reembolso, TipoDeLancamento.Pagamento],
            historico.Itens.Select(l => l.Tipo));

        var resgate = historico.Itens[0];
        Assert.Equal(600, resgate.Pontos);
        Assert.Equal(18.00m, resgate.ValorEmReais);
        Assert.Equal(cupom.Id, resgate.CupomId);
        Assert.Equal(cupom.ItemMarketplaceId, resgate.ItemMarketplaceId);
        Assert.Equal("Emitido", resgate.Status);

        var reembolso = historico.Itens[1];
        Assert.Equal(140m, reembolso.ValorEmReais);
        Assert.Equal(Hoje.AddDays(-5), reembolso.Momento);
        Assert.Equal(pagamento.Id, reembolso.PagamentoId);
        Assert.Equal("Parcial", reembolso.Status);
    }

    [Fact]
    public async Task Reembolso_SemDataPropria_CaiNaDataDoPagamento()
    {
        // Arrange — estorno anterior a Sprint 4
        var pagamento = Pago(100m, diasAtras: 20);
        pagamento.Estornar(100m);
        typeof(Pagamento).GetProperty(nameof(Pagamento.EstornadoEm))!.SetValue(pagamento, null);

        // Act
        var historico = await Historico(new FiltroHistoricoDaCarteiraDto { Tipo = TipoDeLancamento.Reembolso });

        // Assert
        Assert.Equal(Hoje.AddDays(-20), Assert.Single(historico.Itens).Momento);
    }

    [Theory]
    [InlineData(TipoPagamento.Consulta, "Consulta - Pix")]
    [InlineData(TipoPagamento.Caucao, "Caucao de internacao - Pix")]
    [InlineData(TipoPagamento.SaldoInternacao, "Saldo de internacao - Pix")]
    public async Task Pagamento_DescreveOQueFoiCobrado(TipoPagamento tipo, string descricao)
    {
        // Arrange
        Pago(80m, diasAtras: 1).DefinirTipo(tipo);

        // Act
        var historico = await Historico();

        // Assert
        Assert.Equal(descricao, Assert.Single(historico.Itens).Descricao);
    }

    // ── Filtros ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Filtro_PorPeriodoEStatus_RecortaAntesDePaginar()
    {
        // Arrange
        Pago(100m, diasAtras: 40);
        Pago(50m, diasAtras: 10);
        Pago(70m, diasAtras: 5, StatusPagamento.Pendente);
        Resgatado(300, diasAtras: 8);

        // Act
        var historico = await Historico(new FiltroHistoricoDaCarteiraDto
        {
            De = Hoje.AddDays(-15), Ate = Hoje, Status = "confirmado"
        });

        // Assert — status sem distincao de caixa; o resgate tem status de cupom
        Assert.Equal(50m, Assert.Single(historico.Itens).ValorEmReais);
    }

    [Fact]
    public async Task Filtro_PorTipo_SoResgates()
    {
        // Arrange
        Pago(100m, diasAtras: 3);
        Resgatado(300, diasAtras: 2);
        Resgatado(500, diasAtras: 1);

        // Act
        var historico = await Historico(new FiltroHistoricoDaCarteiraDto { Tipo = TipoDeLancamento.Resgate });

        // Assert
        Assert.All(historico.Itens, l => Assert.Equal(TipoDeLancamento.Resgate, l.Tipo));
        Assert.Equal(2, historico.Total);
    }

    // ── Ordenação e paginação ────────────────────────────────────────────────

    [Fact]
    public async Task Ordenacao_PorValorAsc_ESeguidaEntrePaginas()
    {
        // Arrange
        Pago(300m, 1);
        Pago(100m, 2);
        Pago(200m, 3);
        Resgatado(500, 4); // vale R$ 15,00

        // Act
        var pagina1 = await Historico(paginacao: new Paginacao { Tamanho = 2, Pagina = 1 },
            ordenacao: new Ordenacao { OrdenarPor = "valor", Direcao = "asc" });
        var pagina2 = await Historico(paginacao: new Paginacao { Tamanho = 2, Pagina = 2 },
            ordenacao: new Ordenacao { OrdenarPor = "valor", Direcao = "asc" });

        // Assert
        Assert.Equal(4, pagina1.Total);
        Assert.Equal([15m, 100m, 200m, 300m],
            pagina1.Itens.Concat(pagina2.Itens).Select(l => l.ValorEmReais!.Value));
        Assert.True(pagina1.TemProximaPagina);
        Assert.False(pagina2.TemProximaPagina);
    }

    [Fact]
    public async Task Ordenacao_ForaDaWhitelist_CaiEmMomentoDecrescente()
    {
        // Arrange
        Pago(10m, 3);
        Pago(20m, 1);

        // Act
        var historico = await Historico(ordenacao: new Ordenacao { OrdenarPor = "descricao" });

        // Assert
        Assert.Equal([20m, 10m], historico.Itens.Select(l => l.ValorEmReais!.Value));
    }

    [Fact]
    public async Task Ordenacao_PorMomentoAsc_DoMaisAntigoAoMaisNovo()
    {
        // Arrange
        Pago(10m, 1);
        Pago(20m, 9);

        // Act
        var historico = await Historico(ordenacao: new Ordenacao { OrdenarPor = "momento", Direcao = "asc" });

        // Assert
        Assert.Equal([20m, 10m], historico.Itens.Select(l => l.ValorEmReais!.Value));
    }

    [Fact]
    public async Task Ordenacao_PorValorDesc_DoMaiorAoMenor()
    {
        // Arrange
        Pago(10m, 1);
        Pago(20m, 9);

        // Act
        var historico = await Historico(ordenacao: new Ordenacao { OrdenarPor = "valor", Direcao = "desc" });

        // Assert
        Assert.Equal([20m, 10m], historico.Itens.Select(l => l.ValorEmReais!.Value));
    }

    // ── Resumo ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resumo_ContaOQueSaiuDoBolsoMesmoQuandoFoiEstornado()
    {
        // Arrange
        Pago(200m, 5).Estornar(200m);           // pago e devolvido
        Pago(100m, 4).Estornar(30m);            // pago e devolvido em parte
        Pago(50m, 3);                           // pago
        Pago(70m, 2, StatusPagamento.Pendente); // ainda nao saiu do bolso
        Pago(90m, 1, StatusPagamento.Recusado); // nunca vai sair
        Resgatado(300, 1);
        Resgatado(400, 1);

        // Act
        var resumo = (await Historico()).Resumo;

        // Assert
        Assert.Equal(350m, resumo.TotalPago);
        Assert.Equal(230m, resumo.TotalEstornado);
        Assert.Equal(700, resumo.PontosResgatados);
        Assert.Equal("Simulada", resumo.Liquidacao);
    }

    [Fact]
    public async Task Resumo_SomaOsDescontosDosPagamentosCobrados()
    {
        // Arrange
        var comDesconto = Pago(100m, 2);
        comDesconto.AplicarDesconto(Guid.NewGuid(), 300, 9m, 9m, 0m, FaixaDeFinanciamento.Ate10);
        Pago(50m, 1);

        // Act
        var resumo = (await Historico()).Resumo;

        // Assert — cobrado e o bruto menos o desconto
        Assert.Equal(9m, resumo.TotalDeDescontos);
        Assert.Equal(141m, resumo.TotalPago);
    }

    [Fact]
    public async Task Resumo_ValeParaOFiltroInteiro_NaoParaAPagina()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
            Pago(10m, i);

        // Act
        var historico = await Historico(paginacao: new Paginacao { Tamanho = 2 });

        // Assert
        Assert.Equal(2, historico.Itens.Count);
        Assert.Equal(50m, historico.Resumo.TotalPago);
    }

    // ── Escopo (RN-106) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Historico_DeOutroResponsavel_LancaAcessoNegadoSemLerNada()
    {
        // Arrange
        var outro = Guid.NewGuid();

        // Act
        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterHistoricoAsync(outro, new(), new()));

        // Assert
        _pagamentos.Verify(r => r.ObterPorTutorAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Historico_ComoAdmin_AlcancaQualquerCarteira()
    {
        // Arrange
        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.TutorId).Returns((Guid?)null);
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);
        Pago(10m, 1);

        // Act
        var historico = await Historico();

        // Assert
        Assert.Equal(_tutorId, historico.TutorId);
        Assert.Single(historico.Itens);
    }
}
