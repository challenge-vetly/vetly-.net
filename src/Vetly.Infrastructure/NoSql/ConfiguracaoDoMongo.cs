using Microsoft.Extensions.Configuration;

namespace Vetly.Infrastructure.NoSql;

/// <summary>
/// Conexão com o MongoDB do histórico do avatar (<c>MongoDb:ConnectionString</c>,
/// <c>MongoDb:Database</c>).
///
/// Resolvida no arranque, só quando <c>Adaptadores:DocumentoNoSql = Mongo</c>: string
/// ausente tem de derrubar a subida, e não aparecer como histórico que nunca grava.
/// A string de conexão vem por variável de ambiente (<c>MongoDb__ConnectionString</c>)
/// — no <c>appsettings.json</c> versionado fica só um placeholder, que a
/// <c>GuardaDeSegredos</c> recusa em Produção.
/// </summary>
public sealed class ConfiguracaoDoMongo
{
    /// <summary>Placeholder do <c>appsettings.json</c> versionado.</summary>
    public const string Placeholder = "mongodb://USUARIO:SENHA@localhost:27017";

    /// <summary>Banco usado quando <c>MongoDb:Database</c> não é informado.</summary>
    public const string BancoPadrao = "vetly";

    /// <summary>
    /// Tempo máximo para achar um servidor. O padrão do driver é 30 s: com o Mongo
    /// fora, cada abertura de avatar esperaria meio minuto antes de desistir de gravar
    /// o histórico. Cinco segundos é o mesmo teto das sondas de saúde.
    /// </summary>
    public static readonly TimeSpan TempoParaAcharServidor = TimeSpan.FromSeconds(5);

    /// <summary>String de conexão.</summary>
    public string ConnectionString { get; }

    /// <summary>Nome do banco.</summary>
    public string Banco { get; }

    /// <summary>Lê e valida a configuração.</summary>
    /// <exception cref="InvalidOperationException">String de conexão ausente.</exception>
    public ConfiguracaoDoMongo(IConfiguration config)
    {
        ConnectionString = Preenchido(config["MongoDb:ConnectionString"])
            ?? throw new InvalidOperationException(
                "MongoDb:ConnectionString nao configurada. Com Adaptadores:DocumentoNoSql=Mongo, defina " +
                "MongoDb__ConnectionString por variavel de ambiente.");

        Banco = Preenchido(config["MongoDb:Database"]) ?? BancoPadrao;
    }

    private static string? Preenchido(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
