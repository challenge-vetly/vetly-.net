using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes do consentimento granular do Responsavel (RN-060 a RN-062).
/// Cada finalidade e autorizada e revogada separadamente, com data e hora.
/// </summary>
public class ConsentimentoTutorTests
{
    private static Tutor CriarTutor() => new("Ana", "ana@exemplo.com", "11999998888");

    [Fact]
    public void Tutor_RecemCriado_NaoConsentiuNada()
    {
        var tutor = CriarTutor();

        Assert.All(tutor.Consentimentos(), c => Assert.False(c.Concedido));
        Assert.Null(tutor.DataConsentimento);
    }

    [Theory]
    [InlineData(FinalidadeConsentimento.Atendimento)]
    [InlineData(FinalidadeConsentimento.Lembretes)]
    [InlineData(FinalidadeConsentimento.Compartilhamento)]
    [InlineData(FinalidadeConsentimento.Promocoes)]
    [InlineData(FinalidadeConsentimento.DadosAgregados)]
    public void RegistrarConsentimento_ConcedeUmaFinalidadeSemTocarNasOutras(FinalidadeConsentimento finalidade)
    {
        var tutor = CriarTutor();
        var quando = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);

        tutor.RegistrarConsentimento(finalidade, concedido: true, quando);

        Assert.True(tutor.Consentiu(finalidade));
        Assert.All(
            tutor.Consentimentos().Where(c => c.Finalidade != finalidade),
            c => Assert.False(c.Concedido));
    }

    [Fact]
    public void RegistrarConsentimento_GuardaDataDeConcessaoEDeRevogacao()
    {
        var tutor = CriarTutor();
        var concessao = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var revogacao = new DateTime(2026, 8, 20, 15, 30, 0, DateTimeKind.Utc);

        tutor.RegistrarConsentimento(FinalidadeConsentimento.Promocoes, true, concessao);
        tutor.RegistrarConsentimento(FinalidadeConsentimento.Promocoes, false, revogacao);

        var registro = tutor.Consentimentos().Single(c => c.Finalidade == FinalidadeConsentimento.Promocoes);

        Assert.False(registro.Concedido);
        // A revogacao nao apaga o historico: a data da concessao permanece (RN-062)
        Assert.Equal(concessao, registro.ConcedidoEm);
        Assert.Equal(revogacao, registro.RevogadoEm);
    }

    [Fact]
    public void RegistrarConsentimento_SobrecargaAntiga_ContinuaFuncionando()
    {
        var tutor = CriarTutor();

        tutor.RegistrarConsentimento(atendimento: true, lembretes: false, compartilhamento: true);

        Assert.True(tutor.Consentiu(FinalidadeConsentimento.Atendimento));
        Assert.False(tutor.Consentiu(FinalidadeConsentimento.Lembretes));
        Assert.True(tutor.Consentiu(FinalidadeConsentimento.Compartilhamento));
        Assert.NotNull(tutor.DataConsentimento);
    }

    [Fact]
    public void DefinirSenhaHash_HashVazio_LancaArgumentException()
    {
        var tutor = CriarTutor();

        Assert.Throws<ArgumentException>(() => tutor.DefinirSenhaHash("  "));
    }

    [Fact]
    public void TemCredencial_SoDepoisDeDefinirOHash()
    {
        var tutor = CriarTutor();
        Assert.False(tutor.TemCredencial());

        tutor.DefinirSenhaHash("pbkdf2$sha256$210000$c2FsdA==$aGFzaA==");

        Assert.True(tutor.TemCredencial());
    }
}
