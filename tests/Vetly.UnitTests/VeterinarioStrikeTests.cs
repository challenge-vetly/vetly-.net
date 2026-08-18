using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios de dominio puro para os strikes de reputacao do Veterinario.
/// Cobre o limiar de suspensao (3 strikes em 90 dias — RN-067).
/// </summary>
public class VeterinarioStrikeTests
{
    private static Veterinario CriarVeterinario() =>
        new("Dr. Vet", new Crmv("12345-SP"), "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

    [Fact]
    public void RegistrarStrike_PrimeiroEsegundoStrike_NaoSuspende()
    {
        var vet = CriarVeterinario();
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        vet.RegistrarStrike(agora, "motivo 1");
        vet.RegistrarStrike(agora.AddDays(10), "motivo 2");

        Assert.Null(vet.SuspensoAte);
        Assert.False(vet.EstaSuspenso(agora.AddDays(10)));
    }

    [Fact]
    public void RegistrarStrike_TerceiroStrikeDentroDe90Dias_SuspendePor7Dias()
    {
        var vet = CriarVeterinario();
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        vet.RegistrarStrike(agora, "motivo 1");
        vet.RegistrarStrike(agora.AddDays(30), "motivo 2");
        vet.RegistrarStrike(agora.AddDays(89), "motivo 3");

        Assert.Equal(agora.AddDays(89).AddDays(7), vet.SuspensoAte);
        Assert.True(vet.EstaSuspenso(agora.AddDays(89)));
        Assert.False(vet.EstaSuspenso(agora.AddDays(89).AddDays(8))); // apos os 7 dias, suspensao acaba
    }

    [Fact]
    public void RegistrarStrike_TerceiroStrikeForaDaJanelaDe90Dias_NaoSuspende()
    {
        var vet = CriarVeterinario();
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        vet.RegistrarStrike(agora, "motivo 1");
        vet.RegistrarStrike(agora.AddDays(10), "motivo 2");
        // Mais de 90 dias depois do primeiro strike — so 1 dos 3 ainda esta na janela
        vet.RegistrarStrike(agora.AddDays(150), "motivo 3");

        Assert.Null(vet.SuspensoAte);
        Assert.Equal(1, vet.StrikesNaJanela(agora.AddDays(150)));
    }

    [Fact]
    public void RegistrarStrike_HistoricoNuncaEApagado()
    {
        var vet = CriarVeterinario();
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        vet.RegistrarStrike(agora, "motivo 1");
        vet.RegistrarStrike(agora.AddDays(150), "motivo 2");

        Assert.Equal(2, vet.StrikesAtivos.Count); // historico completo preservado, mesmo com so 1 na janela
    }
}
