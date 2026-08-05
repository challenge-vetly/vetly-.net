using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.UnitTests;

/// <summary>Testes unitarios de dominio puro para PontosFidelidade (RN-070/074/075).</summary>
public class PontosFidelidadeTests
{
    private static readonly DateTime Data = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PontosFidelidade CriarLancamento(int pontos = 50) =>
        new(Guid.NewGuid(), Guid.NewGuid(), OrigemPontos.ObrigacaoCumprida, pontos, Data);

    [Fact]
    public void Ctor_PontosZeroOuNegativo_LancaDomainExceptionFIDELIDADE001()
    {
        var ex = Assert.Throws<DomainException>(
            () => new PontosFidelidade(Guid.NewGuid(), Guid.NewGuid(), OrigemPontos.ConsultaAvulsa, 0, Data));

        Assert.Equal("FIDELIDADE-001", ex.Codigo);
    }

    [Fact]
    public void Ctor_DefineExpiraEmComoDozeMesesAposData()
    {
        var lancamento = CriarLancamento();

        Assert.Equal(Data.AddMonths(12), lancamento.ExpiraEm);
    }

    [Fact]
    public void Valido_DentroDoPrazoENaoEstornado_RetornaTrue()
    {
        var lancamento = CriarLancamento();

        Assert.True(lancamento.Valido(Data.AddMonths(11)));
    }

    [Fact]
    public void Valido_AposExpiracao_RetornaFalse()
    {
        var lancamento = CriarLancamento();

        Assert.False(lancamento.Valido(Data.AddMonths(12).AddDays(1)));
    }

    [Fact]
    public void Valido_NoLimiteExatoDeDozeMeses_AindaValido()
    {
        var lancamento = CriarLancamento();

        Assert.True(lancamento.Valido(Data.AddMonths(12)));
    }

    [Fact]
    public void Estornar_LancamentoValido_ParaDeSerValidoImediatamente()
    {
        var lancamento = CriarLancamento();

        lancamento.Estornar();

        Assert.True(lancamento.Estornado);
        Assert.False(lancamento.Valido(Data.AddDays(1)));
    }
}
