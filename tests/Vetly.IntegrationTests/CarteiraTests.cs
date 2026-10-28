using System.Net;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using static Vetly.IntegrationTests.AtoresDeTeste;

namespace Vetly.IntegrationTests;

/// <summary>
/// Histórico unificado da carteira pela API (RN-115): o envelope paginado com
/// resumo, os links de origem com HATEOAS e o escopo do Responsável.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class CarteiraTests
{
    private readonly HttpClient _client;
    private readonly VetlyWebApplicationFactory _factory;

    public CarteiraTests(VetlyWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<(Responsavel Ana, Pagamento Pagamento)> AnaComPagamentoEstornadoAsync()
    {
        var ana = await CriarResponsavelAsync(_client, "Ana");

        var pagamento = new Pagamento(ana.TutorId, 180m, MeioPagamento.Pix, consultaId: Guid.NewGuid());
        pagamento.Confirmar();
        pagamento.Estornar(180m);

        await SemearAsync(_factory, pagamento,
            MovimentoDePontos.PorServicoPago(ana.TutorId, Guid.NewGuid(), 500m, TierFidelidade.Bronze));

        return (ana, pagamento);
    }

    [Fact]
    public async Task Historico_TrazOsTresTiposComResumoDoFiltro()
    {
        // Arrange — pagamento estornado e um resgate legado pelo app antigo
        var (ana, _) = await AnaComPagamentoEstornadoAsync();
        await EnviarAsync(_client, HttpMethod.Post, "/api/fidelidade/resgates", ana.Token,
            """{"itemRef":"legado","itemNome":"Racao","categoria":"Alimentacao","pontos":100}""");

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Get, $"/api/tutores/{ana.TutorId}/carteira/historico", ana.Token);
        var corpo = await JsonDe(resposta);

        // Assert
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(3, corpo.GetProperty("total").GetInt32());
        var tipos = corpo.GetProperty("itens").EnumerateArray().Select(l => l.GetProperty("tipo").GetString()).ToList();
        Assert.Contains("Pagamento", tipos);
        Assert.Contains("Reembolso", tipos);
        Assert.Contains("Resgate", tipos);

        var resumo = corpo.GetProperty("resumo");
        Assert.Equal(180m, resumo.GetProperty("totalPago").GetDecimal());
        Assert.Equal(180m, resumo.GetProperty("totalEstornado").GetDecimal());
        Assert.Equal(100, resumo.GetProperty("pontosResgatados").GetInt32());
    }

    [Fact]
    public async Task HistoricoComHalJson_LevaOResumoEOsLinksDeOrigem()
    {
        // Arrange
        var (ana, pagamento) = await AnaComPagamentoEstornadoAsync();

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Get,
            $"/api/tutores/{ana.TutorId}/carteira/historico?tipo=Reembolso", ana.Token, accept: Hal);
        var corpo = await JsonDe(resposta);

        // Assert
        Assert.Equal(Hal, resposta.Content.Headers.ContentType!.MediaType);
        Assert.Equal(180m, corpo.GetProperty("resumo").GetProperty("totalEstornado").GetDecimal());

        var item = Assert.Single(corpo.GetProperty("itens").EnumerateArray());
        Assert.Equal(["pagamento", "consulta"], Rels(item.GetProperty("_links")));
        Assert.Equal($"/api/pagamentos/{pagamento.Id}",
            item.GetProperty("_links")[0].GetProperty("href").GetString());
        Assert.Contains("tipo=Reembolso", corpo.GetProperty("_links")[0].GetProperty("href").GetString());
    }

    [Fact]
    public async Task HistoricoDeOutroResponsavel_Retorna403()
    {
        // Arrange
        var (ana, _) = await AnaComPagamentoEstornadoAsync();
        var bruno = await CriarResponsavelAsync(_client, "Bruno");

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Get, $"/api/tutores/{ana.TutorId}/carteira/historico", bruno.Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Historico_OrdenadoPorValorEmDuasPaginas_SegueAOrdem()
    {
        // Arrange — tres pagamentos de valores distintos
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var pagamentos = new[] { 90m, 30m, 60m }.Select(v =>
        {
            var p = new Pagamento(ana.TutorId, v, MeioPagamento.Pix);
            p.Confirmar();
            return p;
        }).ToArray<object>();
        await SemearAsync(_factory, pagamentos);

        // Act
        var rota = $"/api/tutores/{ana.TutorId}/carteira/historico?ordenarPor=valor&direcao=desc&tamanho=2";
        var pagina1 = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, rota + "&pagina=1", ana.Token));
        var pagina2 = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, rota + "&pagina=2", ana.Token));

        // Assert
        var valores = pagina1.GetProperty("itens").EnumerateArray()
            .Concat(pagina2.GetProperty("itens").EnumerateArray())
            .Select(l => l.GetProperty("valorEmReais").GetDecimal());
        Assert.Equal([90m, 60m, 30m], valores);
    }
}
