using System.Diagnostics;
using Serilog.Context;

namespace Vetly.API.Middlewares;

/// <summary>
/// Primeiro middleware do pipeline: estabelece o identificador de correlacao da
/// requisicao e o costura nos tres lugares que precisam falar a mesma lingua — o
/// <b>log</b>, o <b>trace</b> e a <b>resposta ao cliente</b>.
/// </summary>
/// <remarks>
/// <para>
/// O problema que isto resolve e concreto. Um Responsavel abre um chamado dizendo "deu
/// erro ao pagar as 14h32". Sem correlacao, a investigacao comeca varrendo log por
/// horario e torcendo para nao haver duas tentativas no mesmo minuto. Com correlacao,
/// o proprio corpo do erro (<c>ProblemDetails.correlationId</c>, ver
/// <see cref="ExceptionHandlingMiddleware"/>) carrega a chave que abre <i>todas</i> as
/// linhas daquela requisicao e o trace completo dela no backend de tracing.
/// </para>
/// <para>
/// A escolha do valor segue uma ordem deliberada:
/// </para>
/// <list type="number">
///   <item><description>o cabecalho <c>X-Correlation-Id</c> enviado pelo cliente, se
///   vier — e assim que o app mobile amarra a jornada dele a nossa;</description></item>
///   <item><description>o <c>TraceId</c> do W3C Trace Context da
///   <see cref="Activity"/> corrente — o mesmo id que o OpenTelemetry exporta, o que
///   faz log e trace se encontrarem sem nenhuma conversao;</description></item>
///   <item><description>o <c>TraceIdentifier</c> do Kestrel, que sempre existe.</description></item>
/// </list>
/// <para>
/// Depois de decidido, o valor e escrito de volta em
/// <see cref="HttpContext.TraceIdentifier"/>. Isso e o que faz o middleware de excecao,
/// que ja lia dali, passar a devolver o id correlacionavel sem precisar de nenhuma
/// alteracao — e a razao de este middleware vir antes dele.
/// </para>
/// <para>
/// O <c>LogContext.Push</c> e o mecanismo do Serilog para propriedade ambiente: tudo
/// que for logado dentro do <c>using</c>, em qualquer camada e em qualquer profundidade
/// da pilha, sai carimbado com <c>CorrelationId</c> e <c>TraceId</c> sem que nenhum
/// servico precise receber esses valores por parametro.
/// </para>
/// </remarks>
public sealed class CorrelationIdMiddleware
{
    /// <summary>Cabecalho de entrada e de saida do identificador de correlacao.</summary>
    public const string Cabecalho = "X-Correlation-Id";

    /// <summary>Tamanho maximo aceito de um id vindo do cliente.</summary>
    /// <remarks>
    /// O valor entra em toda linha de log e volta como cabecalho de resposta: sem limite,
    /// um cliente poderia inflar o log com megabytes por requisicao. 128 caracteres
    /// cobrem GUID, trace id W3C e qualquer id de correlacao razoavel.
    /// </remarks>
    private const int TamanhoMaximo = 128;

    private readonly RequestDelegate _proximo;

    /// <summary>Encadeia o middleware seguinte do pipeline.</summary>
    /// <param name="proximo">Proximo delegate da cadeia.</param>
    public CorrelationIdMiddleware(RequestDelegate proximo) => _proximo = proximo;

    /// <summary>Resolve a correlacao, publica no contexto de log e segue o pipeline.</summary>
    /// <param name="context">Contexto da requisicao HTTP em curso.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var correlacao = Resolver(context);

        // A partir daqui o id e o TraceIdentifier: quem ja lia dali (ProblemDetails,
        // logs de dominio) passa a ver o mesmo valor, sem alteracao nenhuma.
        context.TraceIdentifier = correlacao;

        // Devolvido sempre — inclusive em erro. O suporte pede ao usuario o cabecalho da
        // resposta, e nao um horario aproximado.
        context.Response.Headers[Cabecalho] = correlacao;

        // TraceId separado do CorrelationId de proposito: quando o cliente manda o
        // proprio id, os dois diferem, e e util enxergar os dois lados da costura.
        var traceId = Activity.Current?.TraceId.ToString();

        using (LogContext.PushProperty("CorrelationId", correlacao))
        using (LogContext.PushProperty("TraceId", traceId ?? correlacao))
        {
            // O trace tambem recebe a correlacao: no Jaeger, procurar pelo id que o
            // usuario leu na tela encontra o span exato.
            Activity.Current?.SetTag("vetly.correlation_id", correlacao);

            await _proximo(context);
        }
    }

    /// <summary>
    /// Aplica a ordem de precedencia descrita na documentacao da classe.
    /// </summary>
    /// <param name="context">Contexto da requisicao.</param>
    /// <returns>Identificador de correlacao ja saneado.</returns>
    private static string Resolver(HttpContext context)
    {
        var doCliente = context.Request.Headers[Cabecalho].ToString();

        if (!string.IsNullOrWhiteSpace(doCliente))
        {
            // Cabecalho e entrada do usuario: cortar no limite evita log inflado, e o
            // saneamento evita que quebra de linha injetada quebre o parsing do log.
            var saneado = doCliente.Trim();

            if (saneado.Length > TamanhoMaximo)
                saneado = saneado[..TamanhoMaximo];

            return saneado.Replace('\r', '_').Replace('\n', '_');
        }

        // Sem Activity corrente (nenhum listener registrado — tipico em teste), o
        // TraceIdentifier do Kestrel e a garantia de que nunca se loga sem correlacao.
        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }
}
