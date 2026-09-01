namespace Vetly.IntegrationTests;

/// <summary>
/// <b>Collection Fixture</b> dos testes de integração: uma única instância da
/// <see cref="VetlyWebApplicationFactory"/> compartilhada por todas as classes de teste
/// marcadas com <c>[Collection(ColecaoDaApi.Nome)]</c>.
/// </summary>
/// <remarks>
/// <para>
/// A diferença entre <c>IClassFixture</c> e <c>ICollectionFixture</c> é exatamente o
/// escopo do compartilhamento. Com <c>IClassFixture</c>, o xUnit constrói a fixture uma
/// vez <b>por classe</b> — vinte classes de teste significam vinte hosts ASP.NET Core
/// subindo, cada um com seu container de DI, seu pipeline e seu worker em background.
/// Com <c>ICollectionFixture</c>, é <b>uma</b> para a coleção inteira.
/// </para>
/// <para>
/// O ganho não é só tempo de suíte. Subir o host é o que exercita a composição do
/// container; fazer isso vinte vezes não prova nada a mais do que fazer uma. E há um
/// efeito colateral relevante: como o nome do banco InMemory é estático, todas as
/// factories já compartilhavam o mesmo banco — ou seja, o isolamento que múltiplos hosts
/// aparentavam dar nunca existiu de fato. A coleção torna essa realidade explícita, em
/// vez de deixá-la como armadilha para quem escrever o próximo teste.
/// </para>
/// <para>
/// <b>O contrato que isso impõe:</b> o xUnit não paraleliza classes da mesma coleção,
/// então elas rodam em sequência — mas compartilham estado no banco. Cada teste precisa
/// criar os próprios dados com identificadores únicos (e-mail com <c>Guid</c>, CRMV
/// aleatório) em vez de depender de uma linha específica já existente. É a mesma
/// disciplina que um banco de homologação compartilhado exige.
/// </para>
/// </remarks>
[CollectionDefinition(Nome)]
public sealed class ColecaoDaApi : ICollectionFixture<VetlyWebApplicationFactory>
{
    /// <summary>
    /// Nome da coleção. Constante para que <c>[Collection(...)]</c> nas classes de teste
    /// não dependa de uma string repetida à mão — um erro de digitação ali não falharia
    /// o build, apenas criaria silenciosamente uma segunda coleção com um host próprio.
    /// </summary>
    public const string Nome = "API Vetly (host compartilhado)";

    // Sem corpo por design: a classe só existe para carregar os atributos que declaram
    // a coleção e a fixture. É o padrão prescrito pelo xUnit.
}
