using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>Testes unitarios de dominio puro para <see cref="ConsentimentoLgpd"/> (RN-044).</summary>
public class ConsentimentoLgpdTests
{
    [Fact]
    public void Ativo_RegistroRecemCriado_RetornaTrue()
    {
        var consentimento = new ConsentimentoLgpd(
            Guid.NewGuid(), FinalidadeConsentimento.CompartilhamentoRede, DateTime.UtcNow);

        Assert.True(consentimento.Ativo);
        Assert.Null(consentimento.DataRevogacao);
    }

    [Fact]
    public void Revogar_ConsentimentoAtivo_GravaDataRevogacaoENaoFicaMaisAtivo()
    {
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var consentimento = new ConsentimentoLgpd(Guid.NewGuid(), FinalidadeConsentimento.AtendimentoClinico, agora);

        consentimento.Revogar(agora.AddDays(10));

        Assert.False(consentimento.Ativo);
        Assert.Equal(agora.AddDays(10), consentimento.DataRevogacao);
    }

    [Fact]
    public void Revogar_ChamadoDuasVezes_MantemADataDaPrimeiraRevogacao()
    {
        var agora = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var consentimento = new ConsentimentoLgpd(Guid.NewGuid(), FinalidadeConsentimento.Promocoes, agora);

        consentimento.Revogar(agora.AddDays(5));
        consentimento.Revogar(agora.AddDays(20)); // idempotente — não deve sobrescrever

        Assert.Equal(agora.AddDays(5), consentimento.DataRevogacao);
    }
}
