using Vetly.Domain.Entities;
using Vetly.Domain.Exceptions;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do dominio de Responsavel.
/// Cobre a janela movel de 90 dias de no-shows, o bloqueio de descontos por 60 dias (RN-064)
/// e o credito de cortesia Vetly (RN-065/066).
/// </summary>
public class ResponsavelTests
{
    private static Responsavel CriarResponsavel() =>
        new("Responsavel Teste", "responsavel@teste.com", "11999999999");

    [Fact]
    public void RegistrarNoShow_TresNoShowsDentroDaJanelaDe90Dias_BloqueiaDescontosPor60Dias()
    {
        var responsavel = CriarResponsavel();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        responsavel.RegistrarNoShow(t0);
        responsavel.RegistrarNoShow(t0.AddDays(30));
        responsavel.RegistrarNoShow(t0.AddDays(89));

        Assert.Equal(3, responsavel.ContadorNoShows);
        Assert.Equal(t0.AddDays(89).AddDays(60), responsavel.BloqueadoDescontosAte);
        Assert.Equal(3, responsavel.NoShowsAtivos(t0.AddDays(89)));
    }

    [Fact]
    public void NoShowsAtivos_UltimoNoShowExatamenteNoLimiteDe90Dias_AindaContaComoAtivo()
    {
        var responsavel = CriarResponsavel();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        responsavel.RegistrarNoShow(t0);

        // 90 dias exatos ainda estao dentro da janela (a regra expira apenas acima de 90 dias).
        Assert.Equal(1, responsavel.NoShowsAtivos(t0.AddDays(90)));
    }

    [Fact]
    public void RegistrarNoShow_UltimoNoShowForaDaJanelaDe90Dias_ReiniciaContadorEmVezDeAcumular()
    {
        var responsavel = CriarResponsavel();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        responsavel.RegistrarNoShow(t0);
        responsavel.RegistrarNoShow(t0.AddDays(45));

        // Mais de 90 dias depois do ultimo no-show — janela expirou, contagem reinicia.
        var depoisDaJanela = t0.AddDays(45).AddDays(91);
        responsavel.RegistrarNoShow(depoisDaJanela);

        Assert.Equal(1, responsavel.ContadorNoShows);
        Assert.Null(responsavel.BloqueadoDescontosAte);
        Assert.Equal(0, responsavel.NoShowsAtivos(depoisDaJanela.AddDays(91)));
    }

    [Fact]
    public void CreditarSaldoCreditosVetly_ValorPositivo_SomaAoSaldoExistente()
    {
        var responsavel = CriarResponsavel();

        responsavel.CreditarSaldoCreditosVetly(30m);
        responsavel.CreditarSaldoCreditosVetly(15m);

        Assert.Equal(45m, responsavel.SaldoCreditosVetly);
    }

    [Fact]
    public void CreditarSaldoCreditosVetly_ValorZeroOuNegativo_LancaDomainExceptionRESPONSAVEL002()
    {
        var responsavel = CriarResponsavel();

        var ex = Assert.Throws<DomainException>(() => responsavel.CreditarSaldoCreditosVetly(0m));

        Assert.Equal("RESPONSAVEL-002", ex.Codigo);
    }
}
