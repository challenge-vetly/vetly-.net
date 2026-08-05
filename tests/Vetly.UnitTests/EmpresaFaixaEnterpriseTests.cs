using Vetly.Domain.Entities;

namespace Vetly.UnitTests;

/// <summary>Testes unitarios de dominio puro para a faixa Enterprise da Empresa (RN-092).</summary>
public class EmpresaFaixaEnterpriseTests
{
    private static Empresa CriarEmpresa() => new("Clinica Teste", "Clinica", Guid.NewGuid());

    [Theory]
    [InlineData(1, 599)]
    [InlineData(5, 599)]
    [InlineData(6, 999)]
    [InlineData(10, 999)]
    [InlineData(11, 1699)]
    [InlineData(20, 1699)]
    public void RecalcularFaixaEnterprise_DentroDasFaixasFixas_AplicaValorDaFaixa(int qtdVets, decimal valorEsperado)
    {
        var empresa = CriarEmpresa();

        empresa.RecalcularFaixaEnterprise(qtdVets);

        Assert.Equal(valorEsperado, empresa.FaixaEnterprise);
    }

    [Fact]
    public void RecalcularFaixaEnterprise_AcimaDeVinte_Aplica70PorVetExcedente()
    {
        var empresa = CriarEmpresa();

        empresa.RecalcularFaixaEnterprise(25); // 1699 + 5*70 = 2049

        Assert.Equal(2049m, empresa.FaixaEnterprise);
    }

    [Fact]
    public void RecalcularFaixaEnterprise_NoLimiteExatoDeVinteEUm_JaCobraOExcedente()
    {
        var empresa = CriarEmpresa();

        empresa.RecalcularFaixaEnterprise(21); // 1699 + 1*70 = 1769

        Assert.Equal(1769m, empresa.FaixaEnterprise);
    }

    [Fact]
    public void RecalcularFaixaEnterprise_QuedaDeVetsRebaixaAFaixa()
    {
        var empresa = CriarEmpresa();
        empresa.RecalcularFaixaEnterprise(15); // 1699

        empresa.RecalcularFaixaEnterprise(4); // desvinculação reduz para a faixa base

        Assert.Equal(599m, empresa.FaixaEnterprise);
    }
}
