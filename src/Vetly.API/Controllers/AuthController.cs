using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace Vetly.API.Controllers;

/// <summary>
/// Endpoint de autenticacao. Gera tokens JWT para uso nos demais endpoints.
/// Nao requer autenticacao previa — e o ponto de entrada do sistema.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    public AuthController(IConfiguration config) => _config = config;

    /// <summary>
    /// Gera um token JWT para uso nos demais endpoints.
    /// Role disponíveis: Admin, Veterinario.
    /// </summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult GerarToken([FromBody] TokenRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Role) ||
            (request.Role != "Admin" && request.Role != "Veterinario"))
            return BadRequest(new { erro = "Role invalida. Use 'Admin' ou 'Veterinario'." });

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, request.Usuario),
            new(ClaimTypes.Role, request.Role)
        };

        // entidadeId vincula o token a um registro real (VeterinarioId para role
        // Veterinario, EmpresaId para role Admin) — usado pelas checagens de posse
        // (ex: vet só acessa a própria agenda, admin só acessa a própria empresa).
        if (request.EntidadeId is { } entidadeId)
            claims.Add(new Claim("entidadeId", entidadeId.ToString()));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new TokenResponseDto
        {
            Token = tokenString,
            Role = request.Role,
            ExpiraEm = DateTime.UtcNow.AddHours(8)
        });
    }
}

public sealed class TokenRequestDto
{
    public string Usuario { get; set; } = "usuario-teste";
    public string Role { get; set; } = "Admin";

    /// <summary>
    /// Opcional. Vincula o token a um registro real — VeterinarioId para role
    /// Veterinario, EmpresaId para role Admin — habilitando checagens de posse
    /// nos endpoints que exigem escopo (ex: agenda do próprio vet).
    /// </summary>
    public Guid? EntidadeId { get; set; }
}

public sealed class TokenResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
}
