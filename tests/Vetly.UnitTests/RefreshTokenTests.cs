using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes do refresh token rotativo (§2.2 do documento de engenharia).
/// </summary>
public class RefreshTokenTests
{
    private static RefreshToken CriarToken(DateTime? expiraEm = null) =>
        new(Guid.NewGuid(), TipoUsuario.Tutor, "hash-do-token",
            expiraEm ?? DateTime.UtcNow.AddDays(7));

    [Fact]
    public void Token_HashVazio_NaoEAceito()
    {
        Assert.Throws<ArgumentException>(() =>
            new RefreshToken(Guid.NewGuid(), TipoUsuario.Tutor, "", DateTime.UtcNow.AddDays(7)));
    }

    [Fact]
    public void EstaValido_TokenNovo_EValido()
    {
        var token = CriarToken();

        Assert.True(token.EstaValido(DateTime.UtcNow));
    }

    [Fact]
    public void EstaValido_TokenExpirado_NaoEValido()
    {
        var token = CriarToken(expiraEm: DateTime.UtcNow.AddMinutes(-1));

        Assert.False(token.EstaValido(DateTime.UtcNow));
    }

    [Fact]
    public void Revogar_RegistraARotacaoParaTornarReusoDetectavel()
    {
        var token = CriarToken();
        var substituto = CriarToken();
        var quando = DateTime.UtcNow;

        token.Revogar(quando, substituto.Id);

        Assert.False(token.EstaValido(quando));
        Assert.True(token.Revogado);
        Assert.Equal(quando, token.RevogadoEm);
        Assert.Equal(substituto.Id, token.SubstituidoPorId);
    }

    [Fact]
    public void Revogar_SemSubstituto_ECasoDeLogoutOuOffboarding()
    {
        var token = CriarToken();

        token.Revogar(DateTime.UtcNow);

        Assert.True(token.Revogado);
        Assert.Null(token.SubstituidoPorId);
    }
}
