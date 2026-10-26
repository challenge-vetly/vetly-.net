using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Item do catálogo do marketplace (RN-108/RN-109): invariantes do cadastro,
/// disponibilidade e o ciclo da unidade reservada.
/// </summary>
public class ItemMarketplaceTests
{
    private static ItemMarketplace Item(int custo = 300, int? estoque = 5) =>
        new("Shampoo", "Para pele sensivel", CategoriaItem.Higiene, "Pet shop", custo, 59.90m, null, estoque);

    [Fact]
    public void Construtor_DadosValidos_NasceAtivoComDadosNormalizados()
    {
        // Arrange / Act
        var item = new ItemMarketplace("  Shampoo  ", "   ", CategoriaItem.Higiene, " Pet shop ", 300, 59.90m, null, 5);

        // Assert
        Assert.True(item.Ativo);
        Assert.Equal("Shampoo", item.Nome);
        Assert.Equal("Pet shop", item.Fornecedor);
        Assert.Null(item.Descricao);
        Assert.Null(item.AtualizadoEm);
        Assert.NotEqual(Guid.Empty, item.Id);
    }

    [Theory]
    [InlineData("", "Pet shop", 300, 10, 1)]
    [InlineData("Shampoo", " ", 300, 10, 1)]
    [InlineData("Shampoo", "Pet shop", 0, 10, 1)]
    [InlineData("Shampoo", "Pet shop", 300, -1, 1)]
    [InlineData("Shampoo", "Pet shop", 300, 10, -1)]
    [InlineData("Shampoo", "Pet shop", 300, 10, 99)]
    public void Construtor_DadoInvalido_LancaArgumentException(
        string nome, string fornecedor, int custo, int valor, int estoqueOuCategoria)
    {
        // Arrange — o ultimo argumento vira estoque negativo (-1) ou categoria inexistente (99)
        int? estoque = estoqueOuCategoria == -1 ? -1 : 5;
        var categoria = estoqueOuCategoria == 99 ? (CategoriaItem)99 : CategoriaItem.Higiene;

        // Act / Assert
        Assert.ThrowsAny<ArgumentException>(
            () => new ItemMarketplace(nome, null, categoria, fornecedor, custo, valor, null, estoque));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(3, true)]
    [InlineData(0, false)]
    public void Disponivel_DependeDoEstoque(int? estoque, bool esperado)
    {
        // Arrange
        var item = Item(estoque: estoque);

        // Act
        var disponivel = item.Disponivel;

        // Assert
        Assert.Equal(esperado, disponivel);
    }

    [Fact]
    public void Disponivel_ItemDesativado_EFalsoMesmoComEstoque()
    {
        // Arrange
        var item = Item(estoque: 10);

        // Act
        item.Desativar();

        // Assert
        Assert.False(item.Disponivel);
    }

    [Fact]
    public void ReservarUnidade_ComEstoque_Decrementa()
    {
        // Arrange
        var item = Item(estoque: 2);

        // Act
        item.ReservarUnidade();

        // Assert
        Assert.Equal(1, item.Estoque);
    }

    [Fact]
    public void ReservarUnidade_EstoqueIlimitado_PermaneceNulo()
    {
        // Arrange
        var item = Item(estoque: null);

        // Act
        item.ReservarUnidade();

        // Assert
        Assert.Null(item.Estoque);
    }

    [Fact]
    public void ReservarUnidade_SemEstoque_LancaInvalidOperation()
    {
        // Arrange
        var item = Item(estoque: 0);

        // Act / Assert
        Assert.Throws<InvalidOperationException>(item.ReservarUnidade);
    }

    [Fact]
    public void ReservarUnidade_ItemDesativado_LancaInvalidOperation()
    {
        // Arrange
        var item = Item(estoque: 5);
        item.Desativar();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(item.ReservarUnidade);
    }

    [Fact]
    public void DevolverUnidade_ComEstoque_Incrementa()
    {
        // Arrange
        var item = Item(estoque: 0);

        // Act
        item.DevolverUnidade();

        // Assert
        Assert.Equal(1, item.Estoque);
        Assert.True(item.Disponivel);
    }

    [Fact]
    public void DevolverUnidade_EstoqueIlimitado_PermaneceNulo()
    {
        // Arrange
        var item = Item(estoque: null);

        // Act
        item.DevolverUnidade();

        // Assert
        Assert.Null(item.Estoque);
    }

    [Fact]
    public void DesativarEReativar_SaoIdempotentes()
    {
        // Arrange
        var item = Item();

        // Act
        item.Desativar();
        var depoisDeDesativar = item.AtualizadoEm;
        item.Desativar();
        var depoisDeRepetir = item.AtualizadoEm;
        item.Reativar();
        item.Reativar();

        // Assert — repetir nao e erro nem nova alteracao
        Assert.Equal(depoisDeDesativar, depoisDeRepetir);
        Assert.True(item.Ativo);
    }

    [Fact]
    public void Atualizar_AplicaOsDadosEMarcaAAlteracao()
    {
        // Arrange
        var item = Item();

        // Act
        item.Atualizar("Shampoo 1 L", null, CategoriaItem.Higiene, "Outro fornecedor", 450, 99.90m, null, null);

        // Assert
        Assert.Equal("Shampoo 1 L", item.Nome);
        Assert.Equal(450, item.CustoEmPontos);
        Assert.Null(item.Estoque);
        Assert.NotNull(item.AtualizadoEm);
    }

    [Fact]
    public void Atualizar_CustoInvalido_LancaENaoMarcaAlteracao()
    {
        // Arrange
        var item = Item();

        // Act
        Assert.Throws<ArgumentOutOfRangeException>(
            () => item.Atualizar("Shampoo", null, CategoriaItem.Higiene, "Pet shop", -5, 1m, null, null));

        // Assert
        Assert.Null(item.AtualizadoEm);
    }
}
