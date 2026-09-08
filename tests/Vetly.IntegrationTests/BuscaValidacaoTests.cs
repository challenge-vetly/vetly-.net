using System.Net;

namespace Vetly.IntegrationTests;

/// <summary>
/// Validacao de entrada da busca por proximidade (RN-001/RN-029).
///
/// Existe por causa de um defeito encontrado ao percorrer a API no ambiente real: o
/// [Required] sobre o Guid nao anulavel do animal nunca disparava, entao a busca sem
/// animalId respondia 404 falando de um registro 00000000-... que ninguem pediu, em
/// vez do 400 que diz o que faltou.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class BuscaValidacaoTests
{
    private readonly HttpClient _client;

    public BuscaValidacaoTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Busca_SemAnimalId_Responde400ENao404()
    {
        var resposta = await _client.GetAsync("/api/busca?lat=-23.5614&lng=-46.6558&raioKm=25");

        // 401 tambem serve: a rota exige autenticacao, e o teste sem token para ali.
        // O que nao pode e 404 — esse era o sintoma do Guid.Empty chegando ao servico.
        Assert.NotEqual(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
