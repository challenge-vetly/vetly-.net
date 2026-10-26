using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using static Vetly.IntegrationTests.AtoresDeTeste;

namespace Vetly.IntegrationTests;

/// <summary>
/// Marketplace de ponta a ponta (RN-108 a RN-111): o catálogo carregado pela
/// migration, o resgate que só leva o id do item, a validação do QR no
/// estabelecimento e as recusas de estado.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class MarketplaceTests
{
    // Ids da carga inicial (ItemMarketplaceConfiguration)
    private static readonly Guid BanhoETosa = Guid.Parse("6f1c2a10-0003-4c3e-9a51-000000000001");
    private static readonly Guid KitDental = Guid.Parse("6f1c2a10-0003-4c3e-9a51-000000000003");
    private static readonly Guid Shampoo = Guid.Parse("6f1c2a10-0003-4c3e-9a51-000000000002");

    private readonly HttpClient _client;
    private readonly VetlyWebApplicationFactory _factory;

    public MarketplaceTests(VetlyWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Responsável com saldo: um lote de crédito por serviço pago.</summary>
    private async Task<Responsavel> ResponsavelComPontosAsync(decimal valorPago)
    {
        var responsavel = await CriarResponsavelAsync(_client, "Marketplace");
        await SemearAsync(_factory,
            MovimentoDePontos.PorServicoPago(responsavel.TutorId, Guid.NewGuid(), valorPago, TierFidelidade.Bronze));
        return responsavel;
    }

    private async Task<ItemMarketplace> ItemNovoAsync(int custo, int? estoque, bool ativo = true)
    {
        var item = new ItemMarketplace(
            $"Item de teste {Guid.NewGuid():N}", null, CategoriaItem.Alimentacao,
            "Fornecedor de teste", custo, 50m, null, estoque);
        if (!ativo)
            item.Desativar();

        await SemearAsync(_factory, item);
        return item;
    }

    // ── Catálogo ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListarHigienePorCustoDesc_DevolveACargaInicialNaOrdemPedida()
    {
        // Arrange — e a requisicao exata do relatorio da Sprint 4
        var ana = await CriarResponsavelAsync(_client);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Get,
            "/api/marketplace/itens?categoria=Higiene&ordenarPor=custoEmPontos&direcao=desc", ana.Token);
        var corpo = await JsonDe(resposta);

        // Assert — outros testes podem cadastrar itens; a ordem relativa da carga vale
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var itens = corpo.GetProperty("itens").EnumerateArray().ToList();
        Assert.All(itens, i => Assert.Equal("Higiene", i.GetProperty("categoria").GetString()));

        var custos = itens.Select(i => i.GetProperty("custoEmPontos").GetInt32()).ToList();
        Assert.Equal(custos.OrderByDescending(c => c).ToList(), custos);

        var ids = itens.Select(i => i.GetProperty("id").GetGuid()).ToList();
        Assert.True(ids.IndexOf(BanhoETosa) < ids.IndexOf(KitDental));
        Assert.True(ids.IndexOf(KitDental) < ids.IndexOf(Shampoo));
    }

    [Fact]
    public async Task ListarComHalJson_ItemDisponivelOfereceResgatarAoResponsavel()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Get,
            "/api/marketplace/itens?categoria=Higiene", ana.Token, accept: Hal);
        var corpo = await JsonDe(resposta);

        // Assert
        Assert.Equal(Hal, resposta.Content.Headers.ContentType!.MediaType);
        var banho = corpo.GetProperty("itens").EnumerateArray()
            .First(i => i.GetProperty("dados").GetProperty("id").GetGuid() == BanhoETosa);
        Assert.Equal(["self", "resgatar"], Rels(banho.GetProperty("_links")));
    }

    [Fact]
    public async Task ApenasAcessiveis_UsaOSaldoCalculadoNoServidor()
    {
        // Arrange — 450 pontos: cobre os itens de 300 e 400, nao o de 600
        var ana = await ResponsavelComPontosAsync(450m);

        // Act
        var corpo = await JsonDe(await EnviarAsync(_client, HttpMethod.Get,
            "/api/marketplace/itens?categoria=Higiene&apenasAcessiveis=true&tamanho=100", ana.Token));

        // Assert
        var itens = corpo.GetProperty("itens").EnumerateArray().ToList();
        Assert.All(itens, i => Assert.True(i.GetProperty("custoEmPontos").GetInt32() <= 450));
        Assert.DoesNotContain(itens, i => i.GetProperty("id").GetGuid() == BanhoETosa);
    }

    [Fact]
    public async Task DetalheDoItem_ParaOResponsavel_InformaQuantoFalta()
    {
        // Arrange
        var ana = await ResponsavelComPontosAsync(450m);

        // Act
        var corpo = await JsonDe(await EnviarAsync(_client, HttpMethod.Get,
            $"/api/marketplace/itens/{BanhoETosa}", ana.Token));

        // Assert — 600 pontos valem R$ 18,00 (100 = R$ 3,00, RN-049)
        Assert.Equal(450, corpo.GetProperty("saldoAtual").GetInt32());
        Assert.False(corpo.GetProperty("saldoSuficiente").GetBoolean());
        Assert.Equal(150, corpo.GetProperty("pontosQueFaltam").GetInt32());
        Assert.Equal(18.00m, corpo.GetProperty("descontoEmReais").GetDecimal());
    }

    // ── Jornada: resgatar → validar QR ───────────────────────────────────────

    [Fact]
    public async Task Jornada_ResgatarPeloId_ValidarQr_ESegundaValidacaoEConflito()
    {
        // Arrange
        var ana = await ResponsavelComPontosAsync(1000m);
        var item = await ItemNovoAsync(custo: 400, estoque: 5);
        var veterinario = TokenVeterinario(Guid.NewGuid());

        // Act 1 — resgate sem corpo: custo e categoria saem do catalogo
        var resgate = await EnviarAsync(_client, HttpMethod.Post,
            $"/api/marketplace/itens/{item.Id}/resgatar", ana.Token);
        var cupom = await JsonDe(resgate);

        // Assert 1
        Assert.Equal(HttpStatusCode.Created, resgate.StatusCode);
        Assert.Equal(400, cupom.GetProperty("pontosDebitados").GetInt32());
        Assert.Equal(item.Id, cupom.GetProperty("itemMarketplaceId").GetGuid());
        Assert.Equal("Emitido", cupom.GetProperty("status").GetString());

        var estoque = await LerAsync(_factory, db =>
            db.ItensDoMarketplace.AsNoTracking().Where(i => i.Id == item.Id).Select(i => i.Estoque).SingleAsync());
        Assert.Equal(4, estoque);

        var saldo = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, "/api/fidelidade/saldo", ana.Token));
        Assert.Equal(600, saldo.GetProperty("saldo").GetInt32());

        // Act 2 — o estabelecimento le o QR (digitado em minusculas)
        var codigo = cupom.GetProperty("codigoQr").GetString()!;
        var validacao = await EnviarAsync(_client, HttpMethod.Post, "/api/marketplace/cupons/validar",
            veterinario, $$"""{"codigoQr":"{{codigo.ToLowerInvariant()}}"}""");

        // Assert 2
        Assert.Equal(HttpStatusCode.OK, validacao.StatusCode);
        Assert.Equal("Resgatado", (await JsonDe(validacao)).GetProperty("status").GetString());

        // Act 3 — o mesmo cupom de novo
        var repetida = await EnviarAsync(_client, HttpMethod.Post, "/api/marketplace/cupons/validar",
            veterinario, $$"""{"codigoQr":"{{codigo}}"}""");

        // Assert 3
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Equal("MARKETPLACE-001", (await JsonDe(repetida)).GetProperty("codigo").GetString());

        // Act 4 — o cupom aparece no historico da carteira, ja como resgatado
        var historico = await JsonDe(await EnviarAsync(_client, HttpMethod.Get,
            $"/api/tutores/{ana.TutorId}/carteira/historico?tipo=Resgate", ana.Token));

        // Assert 4
        var lancamento = Assert.Single(historico.GetProperty("itens").EnumerateArray());
        Assert.Equal(cupom.GetProperty("id").GetGuid(), lancamento.GetProperty("cupomId").GetGuid());
        Assert.Equal("Resgatado", lancamento.GetProperty("status").GetString());
        Assert.Equal(400, historico.GetProperty("resumo").GetProperty("pontosResgatados").GetInt32());
    }

    [Fact]
    public async Task ResgateLegadoComItemId_IgnoraOsPontosEnviadosPeloCliente()
    {
        // Arrange — o app antigo manda pontos; com itemId, vale o catalogo
        var ana = await ResponsavelComPontosAsync(1000m);
        var item = await ItemNovoAsync(custo: 700, estoque: null);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post, "/api/fidelidade/resgates", ana.Token,
            $$"""{"itemId":"{{item.Id}}","itemRef":"qualquer","categoria":"Higiene","pontos":1}""");
        var cupom = await JsonDe(resposta);

        // Assert
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(700, cupom.GetProperty("pontosDebitados").GetInt32());
        Assert.Equal("Alimentacao", cupom.GetProperty("categoria").GetString());
    }

    [Fact]
    public async Task ResgateLegadoSemItemId_ContinuaFuncionando()
    {
        // Arrange — o app publicado antes da Sprint 4 nao pode quebrar
        var ana = await ResponsavelComPontosAsync(500m);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post, "/api/fidelidade/resgates", ana.Token,
            """{"itemRef":"racao-legada","itemNome":"Racao","categoria":"Alimentacao","pontos":100}""");

        // Assert
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var cupom = await JsonDe(resposta);
        Assert.Equal(JsonValueKind.Null, cupom.GetProperty("itemMarketplaceId").ValueKind);
    }

    // ── Recusas ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ResgatarItemDesativado_Retorna422RN108()
    {
        // Arrange
        var ana = await ResponsavelComPontosAsync(1000m);
        var item = await ItemNovoAsync(custo: 100, estoque: null, ativo: false);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post,
            $"/api/marketplace/itens/{item.Id}/resgatar", ana.Token);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("RN-108", (await JsonDe(resposta)).GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task ResgatarItemSemEstoque_Retorna422ENaoDebitaPontos()
    {
        // Arrange
        var ana = await ResponsavelComPontosAsync(1000m);
        var item = await ItemNovoAsync(custo: 100, estoque: 0);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post,
            $"/api/marketplace/itens/{item.Id}/resgatar", ana.Token);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        var saldo = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, "/api/fidelidade/saldo", ana.Token));
        Assert.Equal(1000, saldo.GetProperty("saldo").GetInt32());
    }

    [Fact]
    public async Task ResgatarComSaldoInsuficiente_Retorna422ENaoReservaEstoque()
    {
        // Arrange
        var ana = await ResponsavelComPontosAsync(100m);
        var item = await ItemNovoAsync(custo: 900, estoque: 3);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post,
            $"/api/marketplace/itens/{item.Id}/resgatar", ana.Token);

        // Assert — a reserva iria no mesmo SaveChanges, que nao aconteceu
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("RN-050", (await JsonDe(resposta)).GetProperty("codigo").GetString());
        var estoque = await LerAsync(_factory, db =>
            db.ItensDoMarketplace.AsNoTracking().Where(i => i.Id == item.Id).Select(i => i.Estoque).SingleAsync());
        Assert.Equal(3, estoque);
    }

    [Fact]
    public async Task ValidarCupomVencido_Retorna422EDevolveAUnidade()
    {
        // Arrange — cupom com validade ja passada, emitido sobre item com estoque 2
        var ana = await CriarResponsavelAsync(_client);
        var item = await ItemNovoAsync(custo: 100, estoque: 2);
        var cupom = new CupomResgate(ana.TutorId, item.Id.ToString(), item.Nome, item.Categoria, 100, 3m, item.Id);
        typeof(CupomResgate).GetProperty(nameof(CupomResgate.ExpiraEm))!
            .SetValue(cupom, DateTime.UtcNow.AddMinutes(-1));
        await SemearAsync(_factory, cupom);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post, "/api/marketplace/cupons/validar",
            TokenAdmin(), $$"""{"codigoQr":"{{cupom.CodigoQr}}"}""");

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
        Assert.Equal("RN-053", (await JsonDe(resposta)).GetProperty("codigo").GetString());
        var estoque = await LerAsync(_factory, db =>
            db.ItensDoMarketplace.AsNoTracking().Where(i => i.Id == item.Id).Select(i => i.Estoque).SingleAsync());
        Assert.Equal(3, estoque);
    }

    [Fact]
    public async Task ValidarCupom_ComoResponsavel_Retorna403()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post, "/api/marketplace/cupons/validar",
            ana.Token, """{"codigoQr":"VETLY-AAAAAAAAAAAA"}""");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Resgatar_ComoVeterinario_Retorna403()
    {
        // Arrange / Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post,
            $"/api/marketplace/itens/{BanhoETosa}/resgatar", TokenVeterinario(Guid.NewGuid()));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    // ── Manutenção do catálogo (Admin) ───────────────────────────────────────

    [Fact]
    public async Task Admin_CriaAtualizaEDesativa_EItemDesativadoSomeParaOResponsavel()
    {
        // Arrange
        var admin = TokenAdmin();
        var ana = await CriarResponsavelAsync(_client);
        const string corpo = """
            {"nome":"Coleira refletiva","categoria":"Higiene","fornecedor":"Pet shop parceiro",
             "custoEmPontos":250,"valorDeReferencia":45.90,"estoque":10}
            """;

        // Act — criar, atualizar e desativar
        var criado = await EnviarAsync(_client, HttpMethod.Post, "/api/marketplace/itens", admin, corpo);
        Assert.True(criado.IsSuccessStatusCode, await criado.Content.ReadAsStringAsync());
        var id = (await JsonDe(criado)).GetProperty("id").GetGuid();

        var atualizado = await EnviarAsync(_client, HttpMethod.Put, $"/api/marketplace/itens/{id}", admin,
            corpo.Replace("250", "280"));
        var desativado = await EnviarAsync(_client, HttpMethod.Delete, $"/api/marketplace/itens/{id}", admin);
        var paraResponsavel = await EnviarAsync(_client, HttpMethod.Get, $"/api/marketplace/itens/{id}", ana.Token);
        var paraAdmin = await EnviarAsync(_client, HttpMethod.Get, $"/api/marketplace/itens/{id}", admin);

        // Assert
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        Assert.Equal($"/api/marketplace/itens/{id}", criado.Headers.Location!.AbsolutePath);
        Assert.Equal(280, (await JsonDe(atualizado)).GetProperty("custoEmPontos").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, desativado.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, paraResponsavel.StatusCode);
        Assert.False((await JsonDe(paraAdmin)).GetProperty("ativo").GetBoolean());
    }

    [Fact]
    public async Task CriarItem_ComoResponsavel_Retorna403()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Post, "/api/marketplace/itens", ana.Token,
            """{"nome":"X","categoria":"Higiene","fornecedor":"Y","custoEmPontos":10,"valorDeReferencia":1}""");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task TaxasDeListagem_ComoAdmin_TrazAsTresCategoriasNaoCobradas()
    {
        // Arrange / Act
        var corpo = await JsonDe(await EnviarAsync(_client, HttpMethod.Get,
            "/api/marketplace/taxas-de-listagem", TokenAdmin()));

        // Assert
        var taxas = corpo.EnumerateArray().ToDictionary(
            t => t.GetProperty("categoria").GetString()!, t => t.GetProperty("valor").GetDecimal());
        Assert.Equal(150m, taxas["Alimentacao"]);
        Assert.Equal(250m, taxas["Medicamentos"]);
        Assert.Equal(100m, taxas["Higiene"]);
        Assert.All(corpo.EnumerateArray(), t => Assert.False(t.GetProperty("cobrada").GetBoolean()));
    }
}
