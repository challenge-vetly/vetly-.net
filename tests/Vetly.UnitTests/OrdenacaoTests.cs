using Vetly.Application.DTOs.Comum;

namespace Vetly.UnitTests;

/// <summary>
/// Ordenação das listagens (<c>?ordenarPor=&amp;direcao=</c>, Sprint 4).
///
/// Mesmo contrato da paginação: valor desconhecido é normalizado, nunca rejeitado —
/// e campo fora da whitelist nunca chega ao repositório.
/// </summary>
public class OrdenacaoTests
{
    private static readonly string[] Permitidos = ["datahora", "status"];

    [Fact]
    public void Resolver_SemParametros_UsaCampoEDirecaoPadrao()
    {
        // Arrange
        var ordenacao = new Ordenacao();

        // Act
        var criterio = ordenacao.Resolver(Permitidos, "datahora", descendentePadrao: true);

        // Assert
        Assert.Equal("datahora", criterio.Campo);
        Assert.True(criterio.Descendente);
        Assert.Equal("desc", criterio.Direcao);
    }

    [Theory]
    [InlineData("dataHora", "datahora")]
    [InlineData("STATUS", "status")]
    [InlineData("  status  ", "status")]
    public void Resolver_CampoDaWhitelist_IgnoraCaixaEEspacos(string pedido, string esperado)
    {
        // Arrange
        var ordenacao = new Ordenacao { OrdenarPor = pedido };

        // Act
        var criterio = ordenacao.Resolver(Permitidos, "datahora", descendentePadrao: true);

        // Assert
        Assert.Equal(esperado, criterio.Campo);
    }

    [Theory]
    [InlineData("senhaHash")]
    [InlineData("tutorId")]
    [InlineData("datahora; DROP TABLE TB_CONSULTA")]
    public void Resolver_CampoForaDaWhitelist_CaiNoPadrao(string pedido)
    {
        // Arrange — campo que existe na entidade mas nao foi liberado, e lixo
        var ordenacao = new Ordenacao { OrdenarPor = pedido };

        // Act
        var criterio = ordenacao.Resolver(Permitidos, "datahora", descendentePadrao: true);

        // Assert
        Assert.Equal("datahora", criterio.Campo);
    }

    [Theory]
    [InlineData("asc", false)]
    [InlineData("ASC", false)]
    [InlineData("desc", true)]
    [InlineData(" Desc ", true)]
    public void Resolver_DirecaoValida_PrevaleceSobreOPadrao(string direcao, bool descendenteEsperado)
    {
        // Arrange
        var ordenacao = new Ordenacao { Direcao = direcao };

        // Act — padrao oposto ao pedido em metade dos casos, para provar que o pedido vence
        var criterio = ordenacao.Resolver(Permitidos, "datahora", descendentePadrao: !descendenteEsperado);

        // Assert
        Assert.Equal(descendenteEsperado, criterio.Descendente);
    }

    [Theory]
    [InlineData("crescente", true)]
    [InlineData("", false)]
    [InlineData(null, true)]
    public void Resolver_DirecaoDesconhecidaOuVazia_UsaADirecaoPadrao(string? direcao, bool padrao)
    {
        // Arrange
        var ordenacao = new Ordenacao { Direcao = direcao };

        // Act
        var criterio = ordenacao.Resolver(Permitidos, "datahora", descendentePadrao: padrao);

        // Assert
        Assert.Equal(padrao, criterio.Descendente);
    }

    [Fact]
    public void Ordenacao_ValoresEmBranco_SaoNormalizadosParaNulo()
    {
        // Arrange / Act
        var ordenacao = new Ordenacao { OrdenarPor = "   ", Direcao = " " };

        // Assert
        Assert.Null(ordenacao.OrdenarPor);
        Assert.Null(ordenacao.Direcao);
    }

    [Fact]
    public void CriterioDeOrdenacao_Ascendente_ExpoeDirecaoAsc()
    {
        // Arrange
        var criterio = new CriterioDeOrdenacao("valor", Descendente: false);

        // Act
        var direcao = criterio.Direcao;

        // Assert
        Assert.Equal("asc", direcao);
    }

    [Fact]
    public void CamposDeOrdenacao_PadroesDeCadaListagem_EstaoNaPropriaWhitelist()
    {
        // Arrange / Act / Assert — um padrao fora da lista nunca seria resolvido
        Assert.Contains(CamposDeOrdenacao.Consultas.DataHora, CamposDeOrdenacao.Consultas.Todos);
        Assert.Contains(CamposDeOrdenacao.Pagamentos.Momento, CamposDeOrdenacao.Pagamentos.Todos);
        Assert.Contains(CamposDeOrdenacao.Marketplace.Nome, CamposDeOrdenacao.Marketplace.Todos);
    }
}
