using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Vetly.Infrastructure.Data;

/// <summary>
/// Consultas EF Core escritas de forma que o provider Oracle consiga traduzir.
///
/// Existe uma única razão para esta classe, e ela é específica o bastante para valer
/// o arquivo: <b>Oracle anterior à 23c não tem tipo booleano em SQL</b>.
/// </summary>
public static class ConsultasCompativeisComOracle
{
    /// <summary>
    /// Verdadeiro quando existe ao menos uma linha que satisfaz
    /// <paramref name="predicado"/> — o que <c>AnyAsync</c> faria, se ele traduzisse.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que não <c>AnyAsync</c>.</b> Chamado na <b>raiz</b> de uma query,
    /// <c>Any()</c> projeta um booleano, e o provider Oracle o traduz assim:
    /// </para>
    /// <code>
    /// SELECT CASE WHEN EXISTS (SELECT 1 FROM "TB_X" WHERE ...) THEN True ELSE False END
    ///   FROM DUAL
    /// </code>
    /// <para>
    /// <c>True</c> e <c>False</c> não existem no SQL do Oracle antes da 23c: o banco os
    /// lê como identificadores e responde <c>ORA-00904: "FALSE": identificador
    /// inválido</c>. A chamada estoura em tempo de execução — não de compilação, e não
    /// no <c>InMemory</c> da suíte de testes, que não traduz SQL nenhum. O defeito só
    /// aparece contra o banco real.
    /// </para>
    /// <para>
    /// <b>Por que a projeção de um número resolve.</b> <c>Select(_ =&gt; 1)</c> devolve
    /// um inteiro, e inteiro o Oracle tem. A comparação acontece em C#, depois que o
    /// valor voltou — nenhum literal booleano chega ao SQL. E
    /// <c>FirstOrDefaultAsync</c> limita a leitura à primeira linha, que é tudo o que
    /// interessa: sem esse corte a consulta varreria todas as linhas que casam com o
    /// predicado só para descobrir que existe pelo menos uma. Linha nenhuma devolve o
    /// <c>default</c> de <c>int</c>, que é zero — daí a comparação com 1.
    /// </para>
    /// <para>
    /// <b>Por que não <c>Take(1).CountAsync()</c>,</b> que resolveria o ORA-00904 do
    /// mesmo jeito: <c>Take</c> sem <c>OrderBy</c> faz o EF Core emitir
    /// <c>RowLimitingOperationWithoutOrderByWarning</c> a cada execução. O aviso é
    /// improcedente aqui — ordem não muda a resposta de "existe alguma?" —, mas esta
    /// consulta roda numa rotina de um em um minuto e na trilha de autorização, e um
    /// WRN improcedente nessa frequência é ruído que esconde o log que importa.
    /// </para>
    /// <para>
    /// <b>O que continua valendo com <c>Any()</c>.</b> Dentro de um <c>Where</c>, como
    /// em <c>Where(a =&gt; contexto.Consultas.Any(c =&gt; c.AnimalId == a.Id))</c>, o
    /// <c>Any</c> vira um <c>EXISTS</c> na cláusula e não projeta booleano nenhum. Essa
    /// forma traduz bem, é mais legível, e <b>deve</b> continuar sendo usada — a troca
    /// aqui vale só para a raiz da query.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">Entidade consultada.</typeparam>
    /// <param name="query">Origem da consulta.</param>
    /// <param name="predicado">Condição que a linha precisa satisfazer.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public static async Task<bool> ExisteAlgumAsync<T>(
        this IQueryable<T> query,
        Expression<Func<T, bool>> predicado,
        CancellationToken cancellationToken = default) =>
        await query.Where(predicado).Select(_ => 1).FirstOrDefaultAsync(cancellationToken) == 1;
}
