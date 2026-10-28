using System.Net;
using System.Text.Json;

namespace Vetly.IntegrationTests;

/// <summary>
/// O documento OpenAPI é contrato publicado (<c>docs/openapi.json</c>) e a fonte do
/// Scalar. Um atributo mal declarado num controller derruba a geração inteira em
/// tempo de requisição — e ninguém percebe até abrir a documentação.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class DocumentoOpenApiTests
{
    private readonly HttpClient _client;

    public DocumentoOpenApiTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private async Task<JsonElement> DocumentoAsync()
    {
        var resposta = await _client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task ListagemDeConsultas_Documenta200EmJsonEHalJson()
    {
        // Arrange
        var documento = await DocumentoAsync();

        // Act
        var conteudo = documento.GetProperty("paths").GetProperty("/api/consultas")
            .GetProperty("get").GetProperty("responses").GetProperty("200").GetProperty("content");

        // Assert — os dois corpos possiveis do mesmo 200, pela negociacao de conteudo
        Assert.True(conteudo.TryGetProperty("application/json", out _));
        Assert.True(conteudo.TryGetProperty("application/hal+json", out _));
    }

    [Fact]
    public async Task ListagemDeConsultas_DocumentaOrdenacaoEOParametroHateoas()
    {
        // Arrange
        var documento = await DocumentoAsync();

        // Act
        var parametros = documento.GetProperty("paths").GetProperty("/api/consultas")
            .GetProperty("get").GetProperty("parameters").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString())
            .ToList();

        // Assert
        Assert.Contains("OrdenarPor", parametros, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Direcao", parametros, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("hateoas", parametros);
    }
}
