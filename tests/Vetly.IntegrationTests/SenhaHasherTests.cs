using Vetly.Infrastructure.Security;

namespace Vetly.IntegrationTests;

/// <summary>
/// Testes do hash de senha PBKDF2-HMAC-SHA256.
/// Fica neste projeto porque a implementacao vive na Infrastructure.
/// </summary>
public class SenhaHasherTests
{
    private static Pbkdf2SenhaHasher CriarHasher() => new();

    [Fact]
    public void GerarHash_ProduzFormatoAutodescritivo()
    {
        var hash = CriarHasher().GerarHash("senha-forte-123");

        var partes = hash.Split('$');

        // pbkdf2 $ sha256 $ iteracoes $ salt $ hash — os parametros viajam junto,
        // para que aumentar o custo no futuro nao invalide as senhas ja cadastradas
        Assert.Equal(5, partes.Length);
        Assert.Equal("pbkdf2", partes[0]);
        Assert.Equal("sha256", partes[1]);
        Assert.Equal("210000", partes[2]);
    }

    [Fact]
    public void GerarHash_NuncaContemASenhaEmClaro()
    {
        var hash = CriarHasher().GerarHash("senha-forte-123");

        Assert.DoesNotContain("senha-forte-123", hash);
    }

    [Fact]
    public void GerarHash_MesmaSenha_ProduzHashesDiferentes()
    {
        var hasher = CriarHasher();

        var primeiro = hasher.GerarHash("senha-forte-123");
        var segundo = hasher.GerarHash("senha-forte-123");

        // Salt aleatorio por senha: dois cadastros com a mesma senha nao se parecem
        Assert.NotEqual(primeiro, segundo);
        Assert.True(hasher.Confere("senha-forte-123", primeiro));
        Assert.True(hasher.Confere("senha-forte-123", segundo));
    }

    [Fact]
    public void Confere_SenhaCorreta_RetornaVerdadeiro()
    {
        var hasher = CriarHasher();
        var hash = hasher.GerarHash("senha-forte-123");

        Assert.True(hasher.Confere("senha-forte-123", hash));
    }

    [Theory]
    [InlineData("senha-errada")]
    [InlineData("Senha-Forte-123")]
    [InlineData("")]
    public void Confere_SenhaIncorreta_RetornaFalso(string tentativa)
    {
        var hasher = CriarHasher();
        var hash = hasher.GerarHash("senha-forte-123");

        Assert.False(hasher.Confere(tentativa, hash));
    }

    [Theory]
    [InlineData("hash-invalido")]
    [InlineData("bcrypt$sha256$1000$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2$sha256$abc$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2$sha256$210000$nao-e-base64!$aGFzaA==")]
    public void Confere_HashArmazenadoInvalido_RetornaFalsoSemLancar(string hashArmazenado)
    {
        // Registro corrompido nao pode derrubar o login com excecao nao tratada
        Assert.False(CriarHasher().Confere("senha-forte-123", hashArmazenado));
    }

    [Fact]
    public void GerarHash_SenhaVazia_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => CriarHasher().GerarHash("   "));
    }
}
