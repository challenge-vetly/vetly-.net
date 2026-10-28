using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>Taxa de listagem por categoria do marketplace (RN-111).</summary>
public class TaxaDeListagemTests
{
    [Theory]
    [InlineData(CategoriaItem.Alimentacao, 150.00)]
    [InlineData(CategoriaItem.Medicamentos, 250.00)]
    [InlineData(CategoriaItem.Higiene, 100.00)]
    public void Para_CadaCategoria_DevolveATaxaDoProduto(CategoriaItem categoria, decimal esperado)
    {
        // Arrange / Act
        var taxa = TaxaDeListagem.Para(categoria);

        // Assert
        Assert.Equal(esperado, taxa);
    }

    [Fact]
    public void Para_TodaCategoriaDoEnum_TemTaxa()
    {
        // Arrange — categoria nova sem taxa quebraria a tabela do Admin com 500
        var categorias = Enum.GetValues<CategoriaItem>();

        // Act
        var taxas = categorias.Select(TaxaDeListagem.Para).ToList();

        // Assert
        Assert.All(taxas, t => Assert.True(t > 0));
    }

    [Fact]
    public void Para_CategoriaDesconhecida_LancaArgumentOutOfRange()
    {
        // Arrange
        var desconhecida = (CategoriaItem)42;

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxaDeListagem.Para(desconhecida));
    }
}
