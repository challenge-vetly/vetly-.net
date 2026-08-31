using Microsoft.Extensions.Configuration;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Security;

/// <summary>Token de serviço lido da configuração (§3.6).</summary>
public class TokenDeServico : ITokenDeServico
{
    private readonly IConfiguration _config;

    public TokenDeServico(IConfiguration config) => _config = config;

    /// <inheritdoc/>
    public string Valor => _config["Servicos:TokenInterno"] ?? string.Empty;
}
