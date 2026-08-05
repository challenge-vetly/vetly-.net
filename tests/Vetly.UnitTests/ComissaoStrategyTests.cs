using Vetly.Application.Strategies.Comissao;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios das 3 strategies de comissao por plano (RN-089).
/// Valida o percentual e a exclusividade de aplicabilidade por plano.
/// </summary>
public class ComissaoStrategyTests
{
    [Fact]
    public void ComissaoBasico_AplicavelSoParaBasico_Com15Porcento()
    {
        var strategy = new ComissaoBasicoStrategy();

        Assert.True(strategy.Aplicavel(PlanoAssinatura.Basico));
        Assert.False(strategy.Aplicavel(PlanoAssinatura.Profissional));
        Assert.False(strategy.Aplicavel(PlanoAssinatura.Enterprise));
        Assert.Equal(15m, strategy.PercentualComissao);
    }

    [Fact]
    public void ComissaoProfissional_AplicavelSoParaProfissional_Com12Porcento()
    {
        var strategy = new ComissaoProfissionalStrategy();

        Assert.True(strategy.Aplicavel(PlanoAssinatura.Profissional));
        Assert.False(strategy.Aplicavel(PlanoAssinatura.Basico));
        Assert.False(strategy.Aplicavel(PlanoAssinatura.Enterprise));
        Assert.Equal(12m, strategy.PercentualComissao);
    }

    [Fact]
    public void ComissaoEnterprise_AplicavelSoParaEnterprise_Com10Porcento()
    {
        var strategy = new ComissaoEnterpriseStrategy();

        Assert.True(strategy.Aplicavel(PlanoAssinatura.Enterprise));
        Assert.False(strategy.Aplicavel(PlanoAssinatura.Basico));
        Assert.False(strategy.Aplicavel(PlanoAssinatura.Profissional));
        Assert.Equal(10m, strategy.PercentualComissao);
    }
}
