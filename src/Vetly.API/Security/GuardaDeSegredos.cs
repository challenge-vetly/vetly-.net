namespace Vetly.API.Security;

/// <summary>
/// Recusa subir em Produção com segredo de exemplo ou fraco.
///
/// <para>
/// O <c>appsettings.json</c> versionado traz placeholders — <c>Jwt:Key</c>,
/// <c>Servicos:TokenInterno</c> e a connection string — para que quem clona o
/// repositório veja o formato esperado. Eles são úteis exatamente até o primeiro
/// deploy: um ambiente que suba sem sobrescrevê-los assina JWT com uma chave que está
/// publicada no repositório e aceita webhook de pagamento com um token que qualquer
/// pessoa lê no GitHub. Não é uma falha hipotética; é o modo de falha padrão de quem
/// esquece uma variável de ambiente.
/// </para>
/// <para>
/// A verificação roda <b>só em Produção</b> e derruba o arranque, na mesma linha do
/// que já acontece com um adaptador desconhecido: configuração errada falha no deploy,
/// não em produção às três da manhã. Em Desenvolvimento e nos testes os placeholders
/// seguem valendo — é para isso que existem.
/// </para>
/// <para>
/// Não é validação de força de senha. É a diferença entre "alguém escolheu este
/// segredo" e "ninguém escolheu segredo nenhum", que é a única que dá para fazer sem
/// palpite.
/// </para>
/// </summary>
public static class GuardaDeSegredos
{
    /// <summary>
    /// Comprimento mínimo da chave de assinatura, em caracteres.
    ///
    /// HMAC-SHA256 exige uma chave de 256 bits, e <c>SymmetricSecurityKey</c> recusa
    /// menos que isso — só que recusa na <b>emissão do primeiro token</b>, não no
    /// arranque. Sem esta guarda, uma chave curta sobe a API inteira e derruba o
    /// primeiro login, que é o pior lugar para descobrir o problema.
    /// </summary>
    public const int TamanhoMinimoDaChaveJwt = 32;

    /// <summary>
    /// Valores que o repositório publica e que, por isso, não são segredo de ninguém.
    ///
    /// A comparação é por igualdade e não por heurística: adivinhar "parece fraco"
    /// produziria falso positivo em chave legítima, e o caso que importa pegar é o
    /// literal — o valor que veio do arquivo versionado sem ninguém tocar.
    /// </summary>
    private static readonly string[] Placeholders =
    [
        "SUA_JWT_SECRET_KEY_COM_MINIMO_32_CARACTERES",
        "DEFINA_UM_TOKEN_DE_SERVICO_LOCALMENTE",
        "User Id=SEU_USUARIO;Password=SUA_SENHA;Data Source=oracle.fiap.com.br:1521/orcl"
    ];

    /// <summary>
    /// Confere os segredos e lança quando o ambiente é Produção e algum deles não foi
    /// definido de verdade.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Com <b>todos</b> os problemas encontrados de uma vez, e não só o primeiro: quem
    /// está resolvendo um deploy quebrado precisa da lista, não de três tentativas.
    /// </exception>
    public static void Conferir(IConfiguration configuracao, IHostEnvironment ambiente)
    {
        if (!ambiente.IsProduction())
            return;

        var problemas = new List<string>();

        var chaveJwt = configuracao["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(chaveJwt))
            problemas.Add("Jwt:Key nao configurada.");
        else if (EhPlaceholder(chaveJwt))
            problemas.Add("Jwt:Key ainda e o valor de exemplo do appsettings.json versionado.");
        else if (chaveJwt.Length < TamanhoMinimoDaChaveJwt)
            problemas.Add(
                $"Jwt:Key tem {chaveJwt.Length} caracteres; HMAC-SHA256 exige ao menos " +
                $"{TamanhoMinimoDaChaveJwt}.");

        var tokenInterno = configuracao["Servicos:TokenInterno"];

        // Ausente nao e problema aqui: sem token as rotas /api/internos/* recusam tudo,
        // que e o comportamento seguro. O que nao pode e o token PUBLICADO valer.
        if (!string.IsNullOrWhiteSpace(tokenInterno) && EhPlaceholder(tokenInterno))
            problemas.Add(
                "Servicos:TokenInterno ainda e o valor de exemplo do appsettings.json versionado. " +
                "Com ele, qualquer pessoa que leia o repositorio confirma pagamento pelo webhook.");

        var conexao = configuracao.GetConnectionString("OracleConnection");

        if (string.IsNullOrWhiteSpace(conexao))
            problemas.Add("ConnectionStrings:OracleConnection nao configurada.");
        else if (EhPlaceholder(conexao))
            problemas.Add("ConnectionStrings:OracleConnection ainda e a de exemplo.");

        if (problemas.Count == 0)
            return;

        throw new InvalidOperationException(
            "A Vetly API nao sobe em Producao com a configuracao atual:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, problemas.Select(p => "  - " + p)) +
            Environment.NewLine +
            "Defina os valores por variavel de ambiente (ex.: Jwt__Key, Servicos__TokenInterno, " +
            "ConnectionStrings__OracleConnection) ou por appsettings.Production.local.json.");
    }

    private static bool EhPlaceholder(string valor) =>
        Placeholders.Contains(valor.Trim(), StringComparer.Ordinal);
}
