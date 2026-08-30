using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Security;

/// <summary>
/// Emissão do token de acesso JWT e do refresh token opaco (§2.2).
///
/// O refresh token é um valor aleatório de 256 bits, não um JWT: ele não carrega
/// informação, só serve de referência à linha em TB_REFRESH_TOKEN. A base guarda
/// apenas o SHA-256 dele.
/// </summary>
public class GeradorDeTokenJwt : IGeradorDeTokenJwt
{
    private readonly IConfiguration _config;

    public GeradorDeTokenJwt(IConfiguration config) => _config = config;

    /// <inheritdoc/>
    public TokenDeAcesso Emitir(
        Guid usuarioId, string nome, string role, TipoUsuario tipoUsuario,
        IEnumerable<Claim>? claimsAdicionais = null)
    {
        var chave = _config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key nao configurada.");

        // Leitura manual em vez de GetValue<T>: evita trazer o pacote Configuration.Binder
        // para a Infrastructure so por causa de um inteiro.
        var horasDeValidade = int.TryParse(_config["Jwt:HorasDeValidade"], out var horas) ? horas : 8;
        var expiraEm = DateTime.UtcNow.AddHours(horasDeValidade);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuarioId.ToString()),
            new(ClaimTypes.Name, nome),
            new(ClaimTypes.Role, role),
            new("tipoUsuario", tipoUsuario.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (claimsAdicionais is not null)
            claims.AddRange(claimsAdicionais);

        var credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiraEm,
            signingCredentials: credenciais);

        return new TokenDeAcesso(new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }

    /// <inheritdoc/>
    public (string Token, string Hash) GerarRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        return (token, CalcularHash(token));
    }

    /// <inheritdoc/>
    public string CalcularHash(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();
}
