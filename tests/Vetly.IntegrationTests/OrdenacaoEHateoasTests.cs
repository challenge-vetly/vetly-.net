using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// Ordenação com whitelist e HATEOAS por negociação de conteúdo (Sprint 4) em
/// <c>/api/consultas</c> e <c>/api/pagamentos</c>.
///
/// <para>
/// Ordenação só vale se atravessar páginas: ordenar a página depois do recorte
/// passaria num teste de uma página só e devolveria lixo na segunda. Por isso o
/// teste de ordem lê duas páginas e compara a concatenação com a ordem esperada.
/// </para>
/// <para>
/// O HATEOAS é opt-in, e os dois lados importam: com <c>application/hal+json</c> os
/// links aparecem; sem ele, o corpo é exatamente o de antes — é o que garante que o
/// app já publicado não quebra.
/// </para>
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class OrdenacaoEHateoasTests
{
    private const string Hal = "application/hal+json";

    private readonly HttpClient _client;
    private readonly VetlyWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public OrdenacaoEHateoasTests(VetlyWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    private sealed record Responsavel(Guid TutorId, string Token);

    /// <summary>
    /// Responsável real, registrado e com consentimento: o portão da RN-060 barra
    /// qualquer token de Tutor sem consentimento gravado.
    /// </summary>
    private async Task<Responsavel> CriarResponsavelAsync()
    {
        var email = $"ordenacao-{Guid.NewGuid():N}@exemplo.com";

        var registro = await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"Responsavel Ordenacao","email":"{{email}}","telefone":"11999998888","senha":"senha-forte-123"}
            """));
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        var sessao = await registro.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = sessao.GetProperty("token").GetString()!;
        var tutorId = sessao.GetProperty("tutorId").GetGuid();

        var consentir = new HttpRequestMessage(HttpMethod.Put, $"/api/tutores/{tutorId}/consentimentos")
        {
            Content = Corpo("""{"consentimentos":[{"finalidade":"Atendimento","concedido":true}]}""")
        };
        consentir.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(consentir)).StatusCode);

        return new Responsavel(tutorId, token);
    }

    private async Task SemearAsync(params object[] entidades)
    {
        using var escopo = _factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<VetlyDbContext>();
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    private static Consulta NovaConsulta(Guid tutorId, DateTime dataHora, StatusConsulta status)
    {
        var consulta = new Consulta(dataHora, ModalidadeAtendimento.Presencial, Guid.NewGuid(), Guid.NewGuid(), tutorId);

        switch (status)
        {
            case StatusConsulta.Confirmada:
                consulta.ConfirmarPagamento();
                break;
            case StatusConsulta.Realizada:
                consulta.ConfirmarPagamento();
                consulta.Realizar();
                break;
            case StatusConsulta.Cancelada:
                consulta.ConfirmarPagamento();
                consulta.Cancelar();
                break;
        }

        return consulta;
    }

    private async Task<HttpResponseMessage> GetAsync(string rota, string token, string? accept = null)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (accept is not null)
            requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        return await _client.SendAsync(requisicao);
    }

    private static string TokenAdmin()
    {
        var chave = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("VetlySecretKey_MustBeAtLeast32CharactersLong!"));
        var token = new JwtSecurityToken(
            issuer: "Vetly",
            audience: "VetlyAPI",
            claims: [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim("role", "Admin")],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static List<string> Rels(JsonElement links) =>
        links.EnumerateArray().Select(l => l.GetProperty("rel").GetString()!).ToList();

    private static string Href(JsonElement links, string rel) =>
        links.EnumerateArray().First(l => l.GetProperty("rel").GetString() == rel)
            .GetProperty("href").GetString()!;

    // ── Ordenação atravessando páginas ───────────────────────────────────────

    [Fact]
    public async Task ListarConsultas_OrdenadoPorDataAscEmDuasPaginas_ConcatenacaoSegueAOrdemSemRepetir()
    {
        // Arrange — cinco consultas inseridas fora de ordem, para a ordem de insercao
        // nao coincidir por acaso com a pedida
        var ana = await CriarResponsavelAsync();
        var baseData = DateTime.UtcNow.Date.AddDays(10);
        var deslocamentos = new[] { 3, 1, 4, 0, 2 };
        var consultas = deslocamentos
            .Select(d => NovaConsulta(ana.TutorId, baseData.AddDays(d), StatusConsulta.Confirmada))
            .ToArray<object>();
        await SemearAsync(consultas);

        var esperado = consultas.Cast<Consulta>().OrderBy(c => c.DataHora).Select(c => c.Id).ToList();

        // Act
        var pagina1 = await (await GetAsync(
            "/api/consultas?ordenarPor=dataHora&direcao=asc&tamanho=3&pagina=1", ana.Token))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var pagina2 = await (await GetAsync(
            "/api/consultas?ordenarPor=dataHora&direcao=asc&tamanho=3&pagina=2", ana.Token))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        var obtido = pagina1.GetProperty("itens").EnumerateArray()
            .Concat(pagina2.GetProperty("itens").EnumerateArray())
            .Select(c => c.GetProperty("id").GetGuid())
            .ToList();

        Assert.Equal(5, pagina1.GetProperty("total").GetInt32());
        Assert.Equal(esperado, obtido);
    }

    [Fact]
    public async Task ListarConsultas_SemOrdenacao_MantemDataDecrescenteComoAntes()
    {
        // Arrange — o padrao anterior a Sprint 4 nao pode mudar para quem nao pede nada
        var ana = await CriarResponsavelAsync();
        var baseData = DateTime.UtcNow.Date.AddDays(20);
        var consultas = new[] { 1, 3, 2 }
            .Select(d => NovaConsulta(ana.TutorId, baseData.AddDays(d), StatusConsulta.Confirmada))
            .ToArray<object>();
        await SemearAsync(consultas);

        // Act
        var corpo = await (await GetAsync("/api/consultas", ana.Token))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        var datas = corpo.GetProperty("itens").EnumerateArray()
            .Select(c => c.GetProperty("dataHora").GetDateTime()).ToList();
        Assert.Equal(datas.OrderByDescending(d => d).ToList(), datas);
    }

    [Fact]
    public async Task ListarConsultas_CampoForaDaWhitelist_CaiNoPadraoEmVezDe400()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        var baseData = DateTime.UtcNow.Date.AddDays(30);
        await SemearAsync(
            NovaConsulta(ana.TutorId, baseData.AddDays(1), StatusConsulta.Confirmada),
            NovaConsulta(ana.TutorId, baseData.AddDays(2), StatusConsulta.Confirmada));

        // Act — "tutorId" existe na entidade, mas nao esta na whitelist
        var resposta = await GetAsync("/api/consultas?ordenarPor=tutorId&direcao=asc", ana.Token);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert — campo ignorado, direcao respeitada sobre o campo padrao
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var datas = corpo.GetProperty("itens").EnumerateArray()
            .Select(c => c.GetProperty("dataHora").GetDateTime()).ToList();
        Assert.Equal(datas.OrderBy(d => d).ToList(), datas);
    }

    [Fact]
    public async Task ListarPagamentos_OrdenadoPorValorDescEmDuasPaginas_ConcatenacaoSegueAOrdem()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        var pagamentos = new[] { 120m, 80m, 300m, 45.5m, 210m }
            .Select(v => new Pagamento(ana.TutorId, v, MeioPagamento.Pix))
            .ToArray<object>();
        await SemearAsync(pagamentos);

        // Act
        var pagina1 = await (await GetAsync(
            "/api/pagamentos?ordenarPor=valor&direcao=desc&tamanho=2&pagina=1", ana.Token))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var pagina2 = await (await GetAsync(
            "/api/pagamentos?ordenarPor=valor&direcao=desc&tamanho=2&pagina=2", ana.Token))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        var valores = pagina1.GetProperty("itens").EnumerateArray()
            .Concat(pagina2.GetProperty("itens").EnumerateArray())
            .Select(p => p.GetProperty("valor").GetDecimal())
            .ToList();

        Assert.Equal([300m, 210m, 120m, 80m], valores);
    }

    [Fact]
    public async Task ListarPagamentos_FiltroPorStatusETipo_SoDevolveOsQueAtendem()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        var confirmadoConsulta = new Pagamento(ana.TutorId, 100m, MeioPagamento.Pix);
        confirmadoConsulta.Confirmar();
        confirmadoConsulta.DefinirTipo(TipoPagamento.Consulta);

        var confirmadoCaucao = new Pagamento(ana.TutorId, 500m, MeioPagamento.Pix);
        confirmadoCaucao.Confirmar();
        confirmadoCaucao.DefinirTipo(TipoPagamento.Caucao);

        var pendente = new Pagamento(ana.TutorId, 70m, MeioPagamento.Pix);
        pendente.DefinirTipo(TipoPagamento.Consulta);

        await SemearAsync(confirmadoConsulta, confirmadoCaucao, pendente);

        // Act
        var corpo = await (await GetAsync(
            "/api/pagamentos?status=Confirmado&tipo=Consulta", ana.Token))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        var ids = corpo.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();
        Assert.Equal([confirmadoConsulta.Id], ids);
        Assert.Equal("Consulta", corpo.GetProperty("itens")[0].GetProperty("tipo").GetString());
    }

    // ── HATEOAS: opt-in ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListarConsultas_SemHalJson_DevolveOCorpoDeSempreSemLinks()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        await SemearAsync(NovaConsulta(ana.TutorId, DateTime.UtcNow.AddDays(5), StatusConsulta.Confirmada));

        // Act
        var resposta = await GetAsync("/api/consultas", ana.Token);
        var texto = await resposta.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal("application/json", resposta.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("_links", texto);
        var corpo = JsonDocument.Parse(texto).RootElement;
        Assert.True(corpo.GetProperty("itens")[0].TryGetProperty("id", out _));
    }

    [Fact]
    public async Task ListarConsultas_ComHalJson_TrazLinksDosItensEDaNavegacaoPreservandoAQuery()
    {
        // Arrange — tres consultas e paginas de uma: ha anterior e proxima na pagina 2
        var ana = await CriarResponsavelAsync();
        var baseData = DateTime.UtcNow.Date.AddDays(40);
        await SemearAsync(
            NovaConsulta(ana.TutorId, baseData.AddDays(1), StatusConsulta.Confirmada),
            NovaConsulta(ana.TutorId, baseData.AddDays(2), StatusConsulta.Confirmada),
            NovaConsulta(ana.TutorId, baseData.AddDays(3), StatusConsulta.Confirmada));

        // Act
        var resposta = await GetAsync(
            "/api/consultas?ordenarPor=dataHora&direcao=asc&tamanho=1&pagina=2", ana.Token, Hal);
        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        Assert.Equal(Hal, resposta.Content.Headers.ContentType!.MediaType);

        var links = corpo.GetProperty("_links");
        Assert.Equal(["self", "primeira", "anterior", "proxima", "ultima"], Rels(links));

        var proxima = Href(links, "proxima");
        Assert.StartsWith("/api/consultas", proxima);
        Assert.Contains("pagina=3", proxima);
        Assert.Contains("ordenarPor=dataHora", proxima);
        Assert.Contains("direcao=asc", proxima);
        Assert.Contains("pagina=1", Href(links, "anterior"));

        var item = corpo.GetProperty("itens")[0];
        Assert.True(item.GetProperty("dados").TryGetProperty("id", out _));
        Assert.Contains("self", Rels(item.GetProperty("_links")));
    }

    [Fact]
    public async Task ObterConsulta_ComQueryHateoasTrue_LigaOsLinksSemTrocarOAccept()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        var consulta = NovaConsulta(ana.TutorId, DateTime.UtcNow.AddDays(5), StatusConsulta.Confirmada);
        await SemearAsync(consulta);

        // Act
        var corpo = await (await GetAsync($"/api/consultas/{consulta.Id}?hateoas=true", ana.Token))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        Assert.Equal(consulta.Id, corpo.GetProperty("dados").GetProperty("id").GetGuid());
        Assert.Equal($"/api/consultas/{consulta.Id}", Href(corpo.GetProperty("_links"), "self"));
    }

    [Fact]
    public async Task RotaSemHateoas_ComAcceptHalJson_RespondeJsonComumSemRotuloEnganoso()
    {
        // Arrange — o formatador hal+json so escreve envelopes; aqui nao ha envelope
        var ana = await CriarResponsavelAsync();

        // Act
        var resposta = await GetAsync($"/api/tutores/{ana.TutorId}", ana.Token, Hal);

        // Assert
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType!.MediaType);
    }

    // ── HATEOAS: links condicionais ao estado e ao perfil ───────────────────

    [Fact]
    public async Task ObterConsultaConfirmada_ComoResponsavel_OfereceCancelarRemarcarESimular()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        var consulta = NovaConsulta(ana.TutorId, DateTime.UtcNow.AddDays(5), StatusConsulta.Confirmada);
        await SemearAsync(consulta);

        // Act
        var corpo = await (await GetAsync($"/api/consultas/{consulta.Id}", ana.Token, Hal))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        var links = corpo.GetProperty("_links");
        Assert.Equal(["self", "simular-cancelamento", "cancelar", "remarcar"], Rels(links));

        var cancelar = links.EnumerateArray().First(l => l.GetProperty("rel").GetString() == "cancelar");
        Assert.Equal("DELETE", cancelar.GetProperty("metodo").GetString());
        Assert.Equal($"/api/consultas/{consulta.Id}/remarcar", Href(links, "remarcar"));
    }

    [Fact]
    public async Task ObterConsultaRealizada_ComoResponsavel_OfereceDocumentosEAvaliar()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        var consulta = NovaConsulta(ana.TutorId, DateTime.UtcNow.AddDays(-2), StatusConsulta.Realizada);
        await SemearAsync(consulta);

        // Act
        var corpo = await (await GetAsync($"/api/consultas/{consulta.Id}", ana.Token, Hal))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        var links = corpo.GetProperty("_links");
        Assert.Equal(["self", "documentos", "avaliar"], Rels(links));
        Assert.Equal($"/api/documentos/consulta/{consulta.Id}", Href(links, "documentos"));
        Assert.Equal($"/api/avaliacoes/consulta/{consulta.Id}", Href(links, "avaliar"));
    }

    [Fact]
    public async Task ObterConsultaRealizada_ComoAdmin_NaoOfereceAvaliar()
    {
        // Arrange — so o Responsavel avalia (RN-055)
        var ana = await CriarResponsavelAsync();
        var consulta = NovaConsulta(ana.TutorId, DateTime.UtcNow.AddDays(-2), StatusConsulta.Realizada);
        await SemearAsync(consulta);

        // Act
        var corpo = await (await GetAsync($"/api/consultas/{consulta.Id}", TokenAdmin(), Hal))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        var rels = Rels(corpo.GetProperty("_links"));
        Assert.Contains("documentos", rels);
        Assert.DoesNotContain("avaliar", rels);
    }

    [Fact]
    public async Task ObterConsultaCancelada_NaoOfereceNenhumaAcao()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        var consulta = NovaConsulta(ana.TutorId, DateTime.UtcNow.AddDays(5), StatusConsulta.Cancelada);
        await SemearAsync(consulta);

        // Act
        var corpo = await (await GetAsync($"/api/consultas/{consulta.Id}", ana.Token, Hal))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        Assert.Equal(["self"], Rels(corpo.GetProperty("_links")));
    }

    [Fact]
    public async Task ObterPagamentoDeConsulta_ProcessarSplitSoApareceParaOAdmin()
    {
        // Arrange
        var ana = await CriarResponsavelAsync();
        var pagamento = new Pagamento(ana.TutorId, 150m, MeioPagamento.Pix, consultaId: Guid.NewGuid());
        await SemearAsync(pagamento);

        // Act
        var doResponsavel = await (await GetAsync($"/api/pagamentos/{pagamento.Id}", ana.Token, Hal))
            .Content.ReadFromJsonAsync<JsonElement>(Json);
        var doAdmin = await (await GetAsync($"/api/pagamentos/{pagamento.Id}", TokenAdmin(), Hal))
            .Content.ReadFromJsonAsync<JsonElement>(Json);

        // Assert
        Assert.Equal(["self", "status", "consulta"], Rels(doResponsavel.GetProperty("_links")));
        Assert.Equal(["self", "status", "consulta", "processar-split"], Rels(doAdmin.GetProperty("_links")));
    }
}
