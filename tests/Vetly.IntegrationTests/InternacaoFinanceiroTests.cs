using System.Net;
using Microsoft.EntityFrameworkCore;

namespace Vetly.IntegrationTests;

/// <summary>
/// Dinheiro da internacao no split (§5.4/§9; RN-070/RN-072/RN-101).
///
/// A caucao e o saldo da alta sao cobrancas de verdade, e entravam no bruto sem
/// comissao nem repasse: o consolidado deixava de fechar e o profissional nunca
/// recebia pela internacao.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class InternacaoFinanceiroTests
{
    private readonly VetlyWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InternacaoFinanceiroTests(VetlyWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CaucaoESaldoDaAlta_SaemComOSplitDoPlanoDoVeterinario()
    {
        var dono = await AtoresDeTeste.CriarResponsavelAsync(_client, "Ana");
        var animalId = await AtoresDeTeste.CriarPetAsync(_client, dono);

        var criarVet = await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Post, "/api/veterinarios",
            AtoresDeTeste.TokenAdmin(), $$"""
                {"nome":"Dra. Plantao","crmv":"{{Random.Shared.Next(10000, 99999)}}-SP","ufAtuacao":"SP",
                 "email":"plantao-{{Guid.NewGuid():N}}@clinica.com","persona":"Autonomo","plano":"Profissional"}
                """);
        Assert.Equal(HttpStatusCode.Created, criarVet.StatusCode);
        var vetId = (await AtoresDeTeste.JsonDe(criarVet)).GetProperty("veterinario").GetProperty("id").GetGuid();
        var tokenVet = AtoresDeTeste.TokenVeterinario(vetId);

        var abrir = await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Post, "/api/internacoes", tokenVet,
            $$"""{"animalId":"{{animalId}}","veterinarioId":"{{vetId}}","valorCaucao":500,"meioPagamento":"Pix"}""");
        Assert.Equal(HttpStatusCode.Created, abrir.StatusCode);
        var internacaoId = (await AtoresDeTeste.JsonDe(abrir)).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Put,
            $"/api/internacoes/{internacaoId}/procedimentos", tokenVet,
            """{"procedimentos":[{"procedimento":"Fluidoterapia","data":"2026-10-26T10:00:00Z","valor":700}]}"""))
            .StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Post,
            $"/api/internacoes/{internacaoId}/alta", tokenVet, "{}")).StatusCode);

        var cobrancas = await AtoresDeTeste.LerAsync(_factory, db => db.Pagamentos
            .Where(p => p.InternacaoId == internacaoId)
            .OrderByDescending(p => p.Valor)
            .ToListAsync());

        // Caucao de 500 e saldo de 200 (700 apurados - 500 de caucao), ambos no
        // Profissional: 12% para a Vetly, o resto ao veterinario autonomo
        Assert.Collection(cobrancas,
            caucao =>
            {
                Assert.Equal(500m, caucao.Valor);
                Assert.Equal(60m, caucao.Comissao);
                Assert.Equal(440m, caucao.Repasse);
                Assert.Equal(vetId, caucao.DestinatarioRepasseId);
            },
            saldo =>
            {
                Assert.Equal(200m, saldo.Valor);
                Assert.Equal(24m, saldo.Comissao);
                Assert.Equal(176m, saldo.Repasse);
                Assert.Equal(vetId, saldo.DestinatarioRepasseId);
            });
    }
}
