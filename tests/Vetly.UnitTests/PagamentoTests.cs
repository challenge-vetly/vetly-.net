using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>Testes unitarios de dominio puro para Pagamento. Cobre RegistrarComissao (RN-089).</summary>
public class PagamentoTests
{
    [Fact]
    public void RegistrarComissao_Consulta150ReaisComissao12Porcento_CalculaValoresCorretos()
    {
        var pagamento = new Pagamento(Guid.NewGuid(), 150.00m, MeioPagamento.Pix, Guid.NewGuid());

        pagamento.RegistrarComissao(12m);

        Assert.Equal(12m, pagamento.PercentualComissao);
        Assert.Equal(18.00m, pagamento.ValorComissao);
        Assert.Equal(132.00m, pagamento.ValorRepasse);
    }

    [Fact]
    public void RegistrarComissao_ValorComArredondamento_ArredondaParaDuasCasas()
    {
        var pagamento = new Pagamento(Guid.NewGuid(), 99.99m, MeioPagamento.Pix, Guid.NewGuid());

        pagamento.RegistrarComissao(15m); // 99.99 * 0.15 = 14.9985 -> arredonda para 15.00

        Assert.Equal(15.00m, pagamento.ValorComissao);
        Assert.Equal(84.99m, pagamento.ValorRepasse);
    }

    [Fact]
    public void Ctor_NovoPagamento_SempreSimulado()
    {
        var pagamento = new Pagamento(Guid.NewGuid(), 100m, MeioPagamento.Pix);

        Assert.True(pagamento.Simulado);
    }
}
