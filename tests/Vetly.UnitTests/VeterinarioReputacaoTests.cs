using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios de dominio puro para o recalculo de reputacao do Veterinario.
/// Cobre a media ponderada por recencia (RN-078): avaliacoes dos ultimos 90 dias pesam 2x.
/// </summary>
public class VeterinarioReputacaoTests
{
    private static Veterinario CriarVeterinario() =>
        new("Dr. Vet", new Crmv("12345-SP"), "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

    [Fact]
    public void RecalcularReputacao_SemAvaliacoes_NotaMediaNulaETotalZero()
    {
        var vet = CriarVeterinario();

        vet.RecalcularReputacao([], DateTime.UtcNow);

        Assert.Null(vet.NotaMedia);
        Assert.Equal(0, vet.TotalAvaliacoes);
    }

    [Fact]
    public void RecalcularReputacao_TodasDentroDe90Dias_MediaSimplesPoisPesosIguais()
    {
        var vet = CriarVeterinario();
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        vet.RecalcularReputacao(
            [(5, agora.AddDays(-10)), (3, agora.AddDays(-20)), (4, agora.AddDays(-30))], agora);

        Assert.Equal(4m, vet.NotaMedia); // (5+3+4)/3, todas com peso 2 -> mesma média
        Assert.Equal(3, vet.TotalAvaliacoes);
    }

    [Fact]
    public void RecalcularReputacao_MisturaRecenteEAntiga_RecentePesaODobro()
    {
        var vet = CriarVeterinario();
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // nota 5 recente (peso 2) + nota 1 antiga (peso 1) => (5*2 + 1*1) / (2+1) = 11/3
        vet.RecalcularReputacao(
            [(5, agora.AddDays(-10)), (1, agora.AddDays(-91))], agora);

        Assert.Equal(11m / 3m, vet.NotaMedia);
        Assert.Equal(2, vet.TotalAvaliacoes);
    }

    [Fact]
    public void RecalcularReputacao_NoLimiteExatoDe90Dias_AindaContaComoRecente()
    {
        var vet = CriarVeterinario();
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        vet.RecalcularReputacao([(4, agora.AddDays(-90))], agora);

        Assert.Equal(4m, vet.NotaMedia);
    }
}
