using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Vetly.Application.DTOs.Comum;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Adapters;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// Geocodificacao simulada sobre a tabela de apoio TB_CEP_COORDENADA (RN-026, §5.6).
/// Usa banco InMemory com o mesmo seed de CEPs da migration.
/// </summary>
public class GeocodificacaoTests
{
    private static VetlyDbContext CriarContextoComSeed()
    {
        var ctx = new VetlyDbContext(new DbContextOptionsBuilder<VetlyDbContext>()
            .UseInMemoryDatabase($"geo_{Guid.NewGuid()}")
            .Options);

        ctx.CepCoordenadas.AddRange(
            new CepCoordenada("01310100", -23.561414m, -46.655881m, "Sao Paulo", "SP"),
            new CepCoordenada("04538133", -23.585000m, -46.685000m, "Sao Paulo", "SP"),
            new CepCoordenada("22071900", -22.971000m, -43.184000m, "Rio de Janeiro", "RJ"));
        ctx.SaveChanges();

        return ctx;
    }

    private static GeocodificacaoAdapterSimulado CriarAdapter(VetlyDbContext ctx) =>
        new(ctx, NullLogger<GeocodificacaoAdapterSimulado>.Instance);

    private static EnderecoDto Endereco(string cep, string cidade = "Sao Paulo", string uf = "SP") => new()
    {
        Cep = cep,
        Logradouro = "Av. Paulista",
        Numero = "1578",
        Bairro = "Bela Vista",
        Cidade = cidade,
        Uf = uf
    };

    [Fact]
    public async Task CepConhecido_ResolvePeloCepSemMarcarRevisao()
    {
        using var ctx = CriarContextoComSeed();

        var coordenada = await CriarAdapter(ctx).GeocodificarAsync(Endereco("01310-100"));

        Assert.True(coordenada.Resolvida);
        Assert.Equal(PrecisaoCoordenada.Cep, coordenada.Precisao);
        Assert.False(coordenada.Revisar);
        Assert.Equal(-23.561414m, coordenada.Latitude);
    }

    [Fact]
    public async Task Cep_ComOuSemMascara_DaNoMesmoLugar()
    {
        using var ctx = CriarContextoComSeed();
        var adapter = CriarAdapter(ctx);

        var comMascara = await adapter.GeocodificarAsync(Endereco("01310-100"));
        var semMascara = await adapter.GeocodificarAsync(Endereco("01310100"));

        Assert.Equal(comMascara.Latitude, semMascara.Latitude);
        Assert.Equal(comMascara.Longitude, semMascara.Longitude);
    }

    [Fact]
    public async Task CepDesconhecido_EmCidadeConhecida_CaiNoCentroEMarcaRevisao()
    {
        using var ctx = CriarContextoComSeed();

        var coordenada = await CriarAdapter(ctx).GeocodificarAsync(Endereco("01999-999"));

        Assert.True(coordenada.Resolvida);
        Assert.Equal(PrecisaoCoordenada.Bairro, coordenada.Precisao);
        // Serve para nao travar o cadastro, mas nao e boa o bastante para o matching
        Assert.True(coordenada.Revisar);

        // Media das duas coordenadas de Sao Paulo do seed
        Assert.Equal(Math.Round((-23.561414m + -23.585000m) / 2, 6), coordenada.Latitude);
    }

    [Fact]
    public async Task CidadeDesconhecida_NaoInventaCoordenada()
    {
        using var ctx = CriarContextoComSeed();

        var coordenada = await CriarAdapter(ctx).GeocodificarAsync(
            Endereco("99999-999", cidade: "Cidade Inexistente", uf: "XX"));

        // Prestador no lugar errado do mapa e pior que prestador sem posicao:
        // sem coordenada ele so nao aparece na busca por proximidade
        Assert.False(coordenada.Resolvida);
        Assert.Equal(PrecisaoCoordenada.Desconhecida, coordenada.Precisao);
        Assert.Null(coordenada.Latitude);
    }

    [Fact]
    public async Task CidadeConhecida_UfErrada_NaoResolve()
    {
        using var ctx = CriarContextoComSeed();

        // "Sao Paulo/RJ" nao existe no seed: cidade e UF sao conferidas juntas
        var coordenada = await CriarAdapter(ctx).GeocodificarAsync(
            Endereco("01999-999", cidade: "Sao Paulo", uf: "RJ"));

        Assert.False(coordenada.Resolvida);
    }

    [Fact]
    public async Task CadaCidade_TemSeuProprioFallback()
    {
        using var ctx = CriarContextoComSeed();

        var carioca = await CriarAdapter(ctx).GeocodificarAsync(
            Endereco("22999-999", cidade: "Rio de Janeiro", uf: "RJ"));

        Assert.Equal(-22.971000m, carioca.Latitude);
        Assert.True(carioca.Revisar);
    }
}
