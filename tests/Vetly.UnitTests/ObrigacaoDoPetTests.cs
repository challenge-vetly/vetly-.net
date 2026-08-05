using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.UnitTests;

/// <summary>Testes unitarios de dominio puro para ObrigacaoDoPet (RN-069/070).</summary>
public class ObrigacaoDoPetTests
{
    private static readonly DateTime DataLimite = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ObrigacaoDoPet CriarObrigacao() =>
        new(Guid.NewGuid(), TipoObrigacao.Vacina, DataLimite);

    [Fact]
    public void MarcarCumprida_ObrigacaoPendente_MarcaComoCumprida()
    {
        var obrigacao = CriarObrigacao();
        var consultaId = Guid.NewGuid();
        var agora = DataLimite.AddDays(-5);

        obrigacao.MarcarCumprida(consultaId, agora);

        Assert.Equal(StatusObrigacao.Cumprida, obrigacao.Status);
        Assert.Equal(consultaId, obrigacao.ConsultaId);
        Assert.Equal(agora, obrigacao.DataCumprimento);
    }

    [Fact]
    public void MarcarCumprida_ObrigacaoJaCumprida_LancaDomainExceptionOBRIGACAO001()
    {
        var obrigacao = CriarObrigacao();
        obrigacao.MarcarCumprida(Guid.NewGuid(), DataLimite.AddDays(-5));

        var ex = Assert.Throws<DomainException>(() => obrigacao.MarcarCumprida(Guid.NewGuid(), DataLimite));

        Assert.Equal("OBRIGACAO-001", ex.Codigo);
    }

    [Fact]
    public void EstaNoPrazo_PendenteAntesDaDataLimite_RetornaTrue()
    {
        var obrigacao = CriarObrigacao();

        Assert.True(obrigacao.EstaNoPrazo(DataLimite.AddDays(-1)));
    }

    [Fact]
    public void EstaNoPrazo_PendenteAposDataLimite_RetornaFalse()
    {
        var obrigacao = CriarObrigacao();

        Assert.False(obrigacao.EstaNoPrazo(DataLimite.AddDays(1)));
    }

    [Fact]
    public void EstaAtrasada_PendenteAposDataLimite_RetornaTrue()
    {
        var obrigacao = CriarObrigacao();

        Assert.True(obrigacao.EstaAtrasada(DataLimite.AddDays(1)));
    }

    [Fact]
    public void EstaAtrasada_JaCumprida_RetornaFalseMesmoAposDataLimite()
    {
        var obrigacao = CriarObrigacao();
        obrigacao.MarcarCumprida(Guid.NewGuid(), DataLimite.AddDays(-1));

        Assert.False(obrigacao.EstaAtrasada(DataLimite.AddDays(10)));
    }
}
