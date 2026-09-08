using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// Guarda contra truncamento silencioso de inteiro no provider Oracle.
///
/// <para>
/// <b>O defeito que isto impede.</b> Declarar <c>HasColumnType("NUMBER(4)")</c> numa
/// propriedade <c>int</c> faz o provider Oracle escolher o mapeamento pela
/// <b>precisão</b>, e uma precisão pequena resolve para um CLR type de um byte. O
/// modelo do EF passa a tratar a propriedade como <c>byte</c> mesmo com a entidade
/// declarada <c>int</c>, e todo valor acima de 255 é truncado módulo 256 <b>na
/// escrita</b>, sem erro nenhum.
/// </para>
/// <para>
/// Aconteceu em produção com a configuração de agenda: 08:00 (480 minutos) foi
/// gravado como 224, que relido vira 03:44; 18:00 (1080) virou 56, ou seja 00:56. A
/// coluna <c>NUMBER(4)</c> comportava os dois — quem truncou foi o mapeamento. Os
/// horários materializados saíram certos porque vinham do objeto em memória, então o
/// sintoma só aparecia relendo a configuração.
/// </para>
/// <para>
/// <b>Por que a suíte não pegava.</b> Os testes de integração rodam sobre
/// <c>UseInMemoryDatabase</c>, que não aplica mapeamento de tipo do Oracle: 480 entra
/// e 480 volta. É a mesma cegueira que já motivou
/// <c>CompatibilidadeComOracleTests</c>, e a defesa é a mesma — inspecionar o modelo
/// construído com o provider real, sem precisar de banco de pé.
/// </para>
/// </summary>
public class MapeamentoNumericoOracleTests
{
    /// <summary>
    /// Modelo construído com o provider Oracle de verdade.
    ///
    /// Construir o modelo não abre conexão, então a string não precisa apontar para
    /// banco nenhum — o que importa é que as decisões de mapeamento sejam as do
    /// Oracle, e não as do InMemory.
    /// </summary>
    private static IModel ModeloOracle()
    {
        var opcoes = new DbContextOptionsBuilder<VetlyDbContext>()
            .UseOracle("User Id=x;Password=y;Data Source=nao-conecta:1521/orcl")
            .Options;

        using var contexto = new VetlyDbContext(opcoes);
        return contexto.Model;
    }

    /// <summary>
    /// Nenhuma propriedade <c>int</c> do domínio pode acabar mapeada para um tipo que
    /// não comporta <c>int</c>.
    /// </summary>
    /// <remarks>
    /// A checagem é sobre a <b>divergência</b> entre o que a entidade declara e o que
    /// o EF resolveu, e não sobre a largura da coluna: uma propriedade que o domínio
    /// diz ser <c>int</c> e o modelo trata como <c>byte</c> está errada mesmo que os
    /// valores de hoje caibam em 255 — o teto é acidental, e o próximo valor legítimo
    /// o rompe em silêncio.
    /// </remarks>
    [Fact]
    public void PropriedadeIntNuncaEMapeadaParaTipoMaisEstreito()
    {
        var estreitas = new List<string>();

        foreach (var tipo in ModeloOracle().GetEntityTypes())
        {
            foreach (var propriedade in tipo.GetProperties())
            {
                // O tipo que a entidade declara, sem o Nullable<> em volta.
                var doDominio = Nullable.GetUnderlyingType(propriedade.ClrType) ?? propriedade.ClrType;

                if (doDominio != typeof(int))
                    continue;

                var mapeamento = propriedade.GetRelationalTypeMapping();
                var doProvider = Nullable.GetUnderlyingType(mapeamento.ClrType) ?? mapeamento.ClrType;

                if (doProvider == typeof(int) || doProvider == typeof(long) || doProvider == typeof(decimal))
                    continue;

                estreitas.Add(
                    $"{tipo.ClrType.Name}.{propriedade.Name}: entidade diz int, " +
                    $"provider resolveu {doProvider.Name} " +
                    $"(coluna {propriedade.GetColumnName()} {mapeamento.StoreType})");
            }
        }

        Assert.True(estreitas.Count == 0,
            $"""
             {estreitas.Count} propriedade(s) int mapeada(s) para um tipo mais estreito:

             {string.Join("\n             ", estreitas)}

             O provider Oracle escolhe o CLR type pela PRECISAO da coluna: NUMBER(3) e
             NUMBER(4) resolvem para byte, e a escrita trunca modulo 256 sem erro.

             Remova o HasColumnType explicito e deixe o EF usar o mapeamento natural de
             int (NUMBER(10)), ou declare uma precisao que comporte a faixa real.
             """);
    }
}
