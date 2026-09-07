using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using Vetly.API.Security;

namespace Vetly.IntegrationTests;

/// <summary>
/// A guarda que recusa subir em Producao com segredo de exemplo (§ Deploy).
///
/// Fica neste projeto porque a classe vive na Vetly.API, e este e o unico projeto de
/// teste que a referencia.
///
/// O que ela impede tem nome: um ambiente que suba sem sobrescrever o
/// <c>appsettings.json</c> versionado assina JWT com uma chave publicada no
/// repositorio e aceita webhook de pagamento com um token que qualquer pessoa le no
/// GitHub. Nao e falha hipotetica — e o modo de falha padrao de quem esquece uma
/// variavel de ambiente.
/// </summary>
public class GuardaDeSegredosTests
{
    /// <summary>Os valores exatos que o appsettings.json versionado publica.</summary>
    private const string ChaveDeExemplo = "SUA_JWT_SECRET_KEY_COM_MINIMO_32_CARACTERES";
    private const string TokenDeExemplo = "DEFINA_UM_TOKEN_DE_SERVICO_LOCALMENTE";
    private const string ConexaoDeExemplo =
        "User Id=SEU_USUARIO;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/orcl";

    private const string ChaveBoa = "chave-de-producao-com-mais-de-32-caracteres";
    private const string ConexaoBoa = "User Id=vetly;Password=real;Data Source=db:1521/orcl";

    private static IConfiguration Configuracao(
        string? chaveJwt = ChaveBoa,
        string? tokenInterno = "token-de-servico-real",
        string? conexao = ConexaoBoa) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = chaveJwt,
                ["Servicos:TokenInterno"] = tokenInterno,
                ["ConnectionStrings:OracleConnection"] = conexao
            })
            .Build();

    private static IHostEnvironment Ambiente(string nome)
    {
        var mock = new Mock<IHostEnvironment>();
        mock.SetupGet(a => a.EnvironmentName).Returns(nome);
        return mock.Object;
    }

    private static IHostEnvironment Producao() => Ambiente(Environments.Production);
    private static IHostEnvironment Desenvolvimento() => Ambiente(Environments.Development);

    // ── Fora de Producao a guarda nao age ────────────────────────────────────

    [Fact]
    public void EmDesenvolvimento_OsPlaceholdersSeguemValendo()
    {
        // E para isso que eles existem: quem clona o repositorio precisa ver o formato
        // esperado e conseguir rodar.
        GuardaDeSegredos.Conferir(
            Configuracao(ChaveDeExemplo, TokenDeExemplo, ConexaoDeExemplo), Desenvolvimento());
    }

    // ── Em Producao, cada segredo de exemplo derruba o arranque ──────────────

    [Fact]
    public void EmProducao_ComTudoDefinido_Sobe()
    {
        GuardaDeSegredos.Conferir(Configuracao(), Producao());
    }

    [Fact]
    public void EmProducao_ChaveJwtDeExemplo_Recusa()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => GuardaDeSegredos.Conferir(Configuracao(chaveJwt: ChaveDeExemplo), Producao()));

        Assert.Contains("Jwt:Key", erro.Message);
    }

    [Fact]
    public void EmProducao_ChaveJwtCurta_Recusa()
    {
        // HMAC-SHA256 exige 256 bits, e SymmetricSecurityKey so recusa na emissao do
        // PRIMEIRO token — o pior lugar para descobrir o problema.
        var erro = Assert.Throws<InvalidOperationException>(
            () => GuardaDeSegredos.Conferir(Configuracao(chaveJwt: "curta-demais"), Producao()));

        Assert.Contains("32", erro.Message);
    }

    [Fact]
    public void EmProducao_TokenInternoDeExemplo_Recusa()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => GuardaDeSegredos.Conferir(Configuracao(tokenInterno: TokenDeExemplo), Producao()));

        Assert.Contains("Servicos:TokenInterno", erro.Message);
    }

    [Fact]
    public void EmProducao_TokenInternoAusente_Sobe()
    {
        // Sem token as rotas /api/internos/* recusam tudo, que e o comportamento
        // seguro. O que nao pode e o token PUBLICADO valer.
        GuardaDeSegredos.Conferir(Configuracao(tokenInterno: null), Producao());
    }

    [Fact]
    public void EmProducao_ConexaoDeExemplo_Recusa()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => GuardaDeSegredos.Conferir(Configuracao(conexao: ConexaoDeExemplo), Producao()));

        Assert.Contains("OracleConnection", erro.Message);
    }

    [Fact]
    public void EmProducao_ReclamaDeTodosDeUmaVez()
    {
        // Quem esta resolvendo um deploy quebrado precisa da lista, nao de tres
        // tentativas.
        var erro = Assert.Throws<InvalidOperationException>(() => GuardaDeSegredos.Conferir(
            Configuracao(ChaveDeExemplo, TokenDeExemplo, ConexaoDeExemplo), Producao()));

        Assert.Contains("Jwt:Key", erro.Message);
        Assert.Contains("Servicos:TokenInterno", erro.Message);
        Assert.Contains("OracleConnection", erro.Message);
    }

    [Fact]
    public void MensagemDeErro_DizComoCorrigir()
    {
        var erro = Assert.Throws<InvalidOperationException>(
            () => GuardaDeSegredos.Conferir(Configuracao(chaveJwt: ChaveDeExemplo), Producao()));

        // Um erro de deploy que nao diz o nome da variavel de ambiente obriga quem le
        // a ir procurar no codigo.
        Assert.Contains("Jwt__Key", erro.Message);
    }
}
