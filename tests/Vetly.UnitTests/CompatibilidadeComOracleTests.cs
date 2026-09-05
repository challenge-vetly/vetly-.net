using System.Text.RegularExpressions;

namespace Vetly.UnitTests;

/// <summary>
/// Guarda contra a reincidência do ORA-00904 (§5, "o que a suíte não cobre").
///
/// <para>
/// Este é um teste de arquitetura: ele lê o código-fonte da <c>Vetly.Infrastructure</c>
/// em vez de exercitar comportamento. Existe porque o defeito que ele impede é
/// <b>invisível para todo o resto da suíte</b> — os testes de integração rodam sobre
/// <c>UseInMemoryDatabase</c>, que não traduz SQL nenhum, então uma consulta que o
/// provider Oracle não consegue traduzir passa verde aqui e estoura em produção.
/// </para>
/// <para>
/// Foi exatamente assim que dois <c>AnyAsync</c> chegaram ao <c>main</c>: um deles na
/// trilha de autorização da colmeia (RN-066), onde a falha não era um dado a menos, era
/// erro em toda leitura de prontuário feita por veterinário sem acesso vigente.
/// </para>
/// </summary>
public class CompatibilidadeComOracleTests
{
    /// <summary>
    /// Operadores que projetam um <b>booleano</b> quando chamados na raiz de uma query.
    ///
    /// O provider Oracle os traduz para <c>CASE WHEN ... THEN True ELSE False END</c>, e
    /// Oracle anterior à 23c não tem tipo booleano em SQL: responde
    /// <c>ORA-00904: "FALSE": identificador inválido</c>.
    /// </summary>
    private static readonly string[] OperadoresQueProjetamBooleano = ["AnyAsync", "AllAsync"];

    /// <summary>
    /// Por que basta procurar o nome, sem tentar identificar o receptor da chamada.
    ///
    /// <para>
    /// <c>AnyAsync</c> e <c>AllAsync</c> são operadores <b>terminais</b>: executam a
    /// query e devolvem <c>Task</c>. Não existe uso válido deles dentro de um
    /// <c>Where</c> — uma chamada assíncrona numa árvore de expressão nem compila. Logo
    /// toda ocorrência do nome está, necessariamente, na raiz de uma consulta, que é
    /// justamente a forma que não traduz.
    /// </para>
    /// <para>
    /// Isso torna a varredura precisa em vez de frágil: não é preciso reconhecer o
    /// receptor (<c>_context.X</c>, <c>_dbSet</c>, uma variável intermediária) nem casar
    /// encadeamentos que se espalham por várias linhas — e essa fragilidade seria real,
    /// porque o pior dos dois casos originais estava escrito em duas linhas
    /// (<c>_context.Consultas</c> numa, <c>.AnyAsync(</c> na seguinte). Uma regex de
    /// receptor por linha teria deixado passar exatamente o que mais importava pegar.
    /// </para>
    /// <para>
    /// O <c>Any()</c> síncrono <b>não</b> entra na varredura, e é proposital: dentro de
    /// um <c>Where</c> ele vira <c>EXISTS</c> na cláusula, sem projetar booleano nenhum.
    /// Traduz bem, é mais legível e deve continuar sendo usado.
    /// </para>
    /// </summary>
    [Fact]
    public void Infrastructure_NaoUsaOperadorQueProjetaBooleanoNaRaizDaQuery()
    {
        var infraestrutura = Path.Combine(RaizDoRepositorio(), "src", "Vetly.Infrastructure");

        Assert.True(Directory.Exists(infraestrutura),
            $"Nao foi possivel localizar o codigo-fonte da Infrastructure em '{infraestrutura}'.");

        var ocorrencias = Directory
            .EnumerateFiles(infraestrutura, "*.cs", SearchOption.AllDirectories)
            .Where(arquivo => !EstaEmSaidaDeBuild(arquivo))
            .SelectMany(Ocorrencias)
            .ToList();

        Assert.True(ocorrencias.Count == 0, MensagemDaFalha(ocorrencias));
    }

    /// <summary>Linhas de um arquivo que citam um operador proibido, já sem comentários.</summary>
    private static IEnumerable<string> Ocorrencias(string arquivo)
    {
        // Os comentarios saem antes da busca: a propria documentacao do
        // ExisteAlgumAsync cita AnyAsync de proposito, para explicar por que ele nao
        // serve — e um guarda que acusa a explicacao do guarda e inutil.
        var linhas = SemComentarios(File.ReadAllText(arquivo)).Split('\n');

        for (var i = 0; i < linhas.Length; i++)
        {
            var linha = linhas[i];

            if (OperadoresQueProjetamBooleano.Any(op => linha.Contains(op, StringComparison.Ordinal)))
                yield return $"{Path.GetFileName(arquivo)}:{i + 1}  {linha.Trim()}";
        }
    }

    /// <summary>Remove comentários de bloco, de linha e de documentação.</summary>
    private static string SemComentarios(string codigo)
    {
        // As quebras de linha dos blocos sao preservadas para que o numero da linha
        // relatado continue batendo com o arquivo real
        var semBloco = Regex.Replace(codigo, @"/\*.*?\*/", m => new string('\n', m.Value.Count(c => c == '\n')),
            RegexOptions.Singleline);

        return Regex.Replace(semBloco, @"//.*?$", string.Empty, RegexOptions.Multiline);
    }

    private static bool EstaEmSaidaDeBuild(string arquivo) =>
        arquivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        arquivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string MensagemDaFalha(IReadOnlyCollection<string> ocorrencias) =>
        $"""
         {ocorrencias.Count} chamada(s) a um operador que projeta booleano na raiz da query:

         {string.Join("\n         ", ocorrencias)}

         Na raiz de uma consulta, AnyAsync/AllAsync viram
             SELECT CASE WHEN EXISTS (...) THEN True ELSE False END FROM DUAL
         e o Oracle anterior a 23c nao tem tipo booleano: responde ORA-00904.
         A suite nao pega isso porque roda sobre InMemory, que nao traduz SQL.

         Troque por ConsultasCompativeisComOracle.ExisteAlgumAsync, que conta em vez de
         projetar booleano. O Any() SINCRONO dentro de um Where continua correto e nao
         precisa mudar — ali ele vira EXISTS na clausula.
         """;

    /// <summary>
    /// Sobe a partir do binário de teste até a pasta que contém a solução.
    ///
    /// O caminho do <c>bin</c> muda com configuração e framework, e fixá-lo com
    /// <c>../../../..</c> quebraria no primeiro <c>Release</c>.
    /// </summary>
    private static string RaizDoRepositorio()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);

        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "Vetly.slnx")))
            pasta = pasta.Parent;

        Assert.NotNull(pasta);
        return pasta.FullName;
    }
}
