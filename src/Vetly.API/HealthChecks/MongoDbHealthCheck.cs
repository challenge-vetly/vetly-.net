using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Vetly.API.HealthChecks;

/// <summary>
/// Verifica o MongoDB do histórico do avatar, quando
/// <c>Adaptadores:DocumentoNoSql = Mongo</c>.
///
/// A sonda é o comando <c>ping</c>: não lê nem escreve dado, e responde só se o
/// servidor aceitou a conexão e a autenticação — que é o que se quer saber.
/// </summary>
/// <remarks>
/// Falha é <see cref="HealthStatus.Degraded"/>, como o Ollama e o Azure Speech: sem o
/// Mongo, o avatar continua sendo calculado e servido (o histórico só deixa de
/// gravar). Tirar a API de rotação por isso derrubaria o app inteiro por um gráfico.
/// </remarks>
public sealed class MongoDbHealthCheck : IHealthCheck
{
    /// <summary>Timeout próprio da sonda. Health check que trava é pior que o que falha.</summary>
    private static readonly TimeSpan TimeoutDaSonda = TimeSpan.FromSeconds(5);

    private readonly IMongoDatabase _banco;

    /// <summary>Usa o mesmo banco que o repositório do histórico.</summary>
    public MongoDbHealthCheck(IMongoDatabase banco) => _banco = banco;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeoutDaSonda);

            await _banco.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: timeout.Token);

            return HealthCheckResult.Healthy($"MongoDB respondeu (banco {_banco.DatabaseNamespace.DatabaseName}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded($"MongoDB nao respondeu em {TimeoutDaSonda.TotalSeconds:0}s.");
        }
        catch (TimeoutException ex)
        {
            // O driver lanca TimeoutException quando nao acha servidor no tempo de
            // selecao configurado — e o caso tipico de Mongo fora do ar
            return HealthCheckResult.Degraded("MongoDB indisponivel: nenhum servidor respondeu.", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Falha ao consultar o MongoDB: {ex.GetType().Name}.", ex);
        }
    }
}
