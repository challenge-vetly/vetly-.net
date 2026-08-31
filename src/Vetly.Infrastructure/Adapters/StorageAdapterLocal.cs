using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Storage em disco local, para desenvolvimento (§2.6, §11).
///
/// Cumpre o mesmo contrato de um bucket S3-compatível: emite URLs temporárias e
/// assinadas, e os bytes nunca passam pelo processo da API por outro caminho. A
/// diferença é onde o arquivo pousa — aqui, uma pasta; em produção, o bucket.
///
/// A assinatura é um HMAC sobre <c>chave + operação + expiração</c>, conferido pela
/// rota que recebe o arquivo. Não é o mesmo algoritmo da AWS, mas tem a mesma
/// propriedade que importa: <b>a URL não pode ser forjada nem reaproveitada depois
/// de expirar</b> — sem isso, qualquer pessoa que descobrisse o padrão da chave
/// leria áudio de consulta alheia.
/// </summary>
public class StorageAdapterLocal : IStorageAdapter
{
    private readonly string _raiz;
    private readonly string _baseUrl;
    private readonly byte[] _segredo;
    private readonly ILogger<StorageAdapterLocal> _logger;

    public StorageAdapterLocal(IConfiguration config, ILogger<StorageAdapterLocal> logger)
    {
        // Chave presente com valor vazio e o caso comum no appsettings de exemplo:
        // tratar como nao configurada evita um CreateDirectory("") que derruba a API
        // inteira no arranque.
        _raiz = Preenchido(config["Storage:Diretorio"])
            ?? Path.Combine(Path.GetTempPath(), "vetly-storage");

        _baseUrl = (Preenchido(config["Storage:BaseUrl"]) ?? "/api/storage").TrimEnd('/');

        var segredo = Preenchido(config["Storage:Segredo"]) ?? Preenchido(config["Jwt:Key"])
            ?? throw new InvalidOperationException("Storage:Segredo nao configurado.");

        _segredo = Encoding.UTF8.GetBytes(segredo);
        _logger = logger;

        Directory.CreateDirectory(_raiz);
    }

    /// <summary>Devolve o valor quando ele tem conteúdo, e nulo quando é vazio ou só espaços.</summary>
    private static string? Preenchido(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;

    /// <inheritdoc/>
    public Task<UrlAssinadaDto> GerarUrlDeUploadAsync(string chave, string contentType, TimeSpan validade) =>
        Task.FromResult(Assinar(chave, "upload", validade));

    /// <inheritdoc/>
    public Task<UrlAssinadaDto> GerarUrlDeLeituraAsync(string chave, TimeSpan validade) =>
        Task.FromResult(Assinar(chave, "leitura", validade));

    /// <inheritdoc/>
    public Task<bool> ExisteAsync(string chave) => Task.FromResult(File.Exists(CaminhoDe(chave)));

    /// <inheritdoc/>
    public Task<long?> ObterTamanhoAsync(string chave)
    {
        var caminho = CaminhoDe(chave);

        return Task.FromResult(File.Exists(caminho) ? new FileInfo(caminho).Length : (long?)null);
    }

    /// <inheritdoc/>
    public Task RemoverAsync(string chave)
    {
        var caminho = CaminhoDe(chave);

        if (File.Exists(caminho))
        {
            File.Delete(caminho);
            _logger.LogInformation("Objeto removido do storage local | chave={Chave}", chave);
        }

        return Task.CompletedTask;
    }

    /// <summary>Grava os bytes recebidos. Usado pela rota que honra a URL de upload.</summary>
    public async Task GravarAsync(string chave, Stream conteudo, CancellationToken cancellationToken = default)
    {
        var caminho = CaminhoDe(chave);
        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);

        await using var arquivo = File.Create(caminho);
        await conteudo.CopyToAsync(arquivo, cancellationToken);
    }

    /// <summary>Abre o objeto para leitura. Usado pela rota que honra a URL de leitura.</summary>
    public Stream Abrir(string chave) => File.OpenRead(CaminhoDe(chave));

    /// <summary>
    /// Confere a assinatura de uma URL. Comparação em tempo fixo, e expiração checada
    /// antes de qualquer acesso a disco.
    /// </summary>
    public bool AssinaturaConfere(string chave, string operacao, long expiraEm, string assinatura)
    {
        if (DateTimeOffset.FromUnixTimeSeconds(expiraEm) < DateTimeOffset.UtcNow)
            return false;

        var esperada = CalcularAssinatura(chave, operacao, expiraEm);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(assinatura ?? string.Empty),
            Encoding.UTF8.GetBytes(esperada));
    }

    private UrlAssinadaDto Assinar(string chave, string operacao, TimeSpan validade)
    {
        var expiraEm = DateTimeOffset.UtcNow.Add(validade);
        var expiraEmUnix = expiraEm.ToUnixTimeSeconds();
        var assinatura = CalcularAssinatura(chave, operacao, expiraEmUnix);

        var url = $"{_baseUrl}/{Uri.EscapeDataString(chave)}" +
                  $"?operacao={operacao}&expiraEm={expiraEmUnix}&assinatura={Uri.EscapeDataString(assinatura)}";

        return new UrlAssinadaDto(url, expiraEm.UtcDateTime);
    }

    private string CalcularAssinatura(string chave, string operacao, long expiraEm) =>
        Convert.ToHexString(HMACSHA256.HashData(_segredo, Encoding.UTF8.GetBytes($"{chave}|{operacao}|{expiraEm}")))
            .ToLowerInvariant();

    /// <summary>
    /// Caminho em disco correspondente à chave. Rejeita chave que tente escapar do
    /// diretório raiz — sem isso, uma chave com <c>..</c> leria arquivo de fora.
    /// </summary>
    private string CaminhoDe(string chave)
    {
        var caminho = Path.GetFullPath(Path.Combine(_raiz, chave.Replace('/', Path.DirectorySeparatorChar)));

        if (!caminho.StartsWith(Path.GetFullPath(_raiz), StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Chave de storage invalida.");

        return caminho;
    }
}
