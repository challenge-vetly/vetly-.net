using Microsoft.Extensions.Logging.Abstractions;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Infrastructure.Adapters;

namespace Vetly.IntegrationTests;

/// <summary>
/// Assinatura pelo nome digitado (RN-087, §5).
///
/// Fica neste projeto porque o adaptador vive na Infrastructure.
/// </summary>
public class AssinaturaAdapterTests
{
    private readonly AssinaturaAdapterNomeDigitado _adapter =
        new(NullLogger<AssinaturaAdapterNomeDigitado>.Instance);

    private static SolicitacaoDeAssinaturaDto Pedido(string? nomeDigitado) => new(
        Guid.NewGuid(), "ReceitaVeterinaria", "Marina Costa Silva", "12345-SP", "SP", nomeDigitado);

    [Fact]
    public async Task Assinatura_ComONomeCorreto_ProduzMetodoECarimbo()
    {
        var assinatura = await _adapter.AssinarAsync(Pedido("Marina Costa Silva"));

        Assert.Equal("NomeDigitado", assinatura.Metodo);
        Assert.Contains("Marina Costa Silva", assinatura.Carimbo);
        Assert.Contains("CRMV 12345-SP/SP", assinatura.Carimbo);
    }

    [Fact]
    public async Task Assinatura_DizNoProprioCarimboOQueNaoHabilita()
    {
        var assinatura = await _adapter.AssinarAsync(Pedido("Marina Costa Silva"));

        // Quem recebe a receita precisa ver isso sem perguntar: omitir seria deixar o
        // documento parecer mais do que e
        Assert.False(assinatura.HabilitaDispensacaoExterna);
        Assert.Contains("Nao habilita dispensacao de medicamento controlado", assinatura.Carimbo);
    }

    [Fact]
    public async Task Assinatura_ComNomeDeOutroProfissional_ERecusada()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => _adapter.AssinarAsync(Pedido("Joao Pereira")));

        Assert.Equal("RN-087", ex.Codigo);
    }

    [Fact]
    public async Task Assinatura_SemNomeDigitado_NaoEAceita()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _adapter.AssinarAsync(Pedido("   ")));
    }

    [Theory]
    [InlineData("marina costa silva")]
    [InlineData("MARINA COSTA SILVA")]
    [InlineData("Marina  Costa   Silva")]
    [InlineData("  Marina Costa Silva  ")]
    public async Task Assinatura_ToleraCaixaEEspacoRepetido(string digitado)
    {
        var assinatura = await _adapter.AssinarAsync(Pedido(digitado));

        Assert.Equal("NomeDigitado", assinatura.Metodo);
    }

    [Fact]
    public async Task Assinatura_ToleraAcentoFaltando()
    {
        var comAcento = new SolicitacaoDeAssinaturaDto(
            Guid.NewGuid(), "Atestado", "Antônio Gonçalves", "54321-MG", "MG", "Antonio Goncalves");

        var assinatura = await _adapter.AssinarAsync(comAcento);

        // Recusar por um acento faltando seria rigor no lugar errado: o que importa e
        // que o profissional escreveu o proprio nome
        Assert.Contains("Antônio Gonçalves", assinatura.Carimbo);
    }
}
