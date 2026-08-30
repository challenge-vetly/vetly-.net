using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.API.Filters;
using Vetly.Application.DTOs.Auth;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.API.Controllers;

/// <summary>
/// Autenticacao e sessao (§3.1). Cadastro do Responsavel, login por e-mail e senha,
/// renovacao com refresh token rotativo e encerramento de sessao.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[IsentoDeConsentimento]        // autenticacao precede o consentimento (RN-060)
[PermitidoAoVetDesativado]     // o vet desligado ainda precisa entrar para pedir o extrato (RN-024)
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;
    private readonly IGeradorDeTokenJwt _gerador;
    private readonly IWebHostEnvironment _ambiente;

    public AuthController(IAuthService service, IGeradorDeTokenJwt gerador, IWebHostEnvironment ambiente)
    {
        _service = service;
        _gerador = gerador;
        _ambiente = ambiente;
    }

    /// <summary>
    /// Cadastra o Responsavel pelo app e ja devolve a sessao (RN-060).
    /// O token vem com <c>consentimentoPendente = true</c>: o app deve levar o
    /// Responsavel a tela de consentimento antes de qualquer acao de negocio.
    /// </summary>
    [HttpPost("registro/tutor")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenEmitidoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarTutor([FromBody] RegistrarTutorDto dto)
    {
        var sessao = await _service.RegistrarTutorAsync(dto);
        return Created(string.Empty, sessao);
    }

    /// <summary>Autentica por e-mail e senha e emite o par de tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenEmitidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Login([FromBody] LoginDto dto) =>
        Ok(await _service.LoginAsync(dto));

    /// <summary>
    /// Renova o acesso rotacionando o refresh token. Reapresentar um token ja usado
    /// derruba todas as sessoes do usuario — e sinal de vazamento.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenEmitidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Renovar([FromBody] RefreshDto dto) =>
        Ok(await _service.RenovarAsync(dto));

    /// <summary>Encerra a sessao revogando o refresh token. Idempotente.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshDto dto)
    {
        await _service.EncerrarSessaoAsync(dto);
        return NoContent();
    }

    /// <summary>Perfil do usuario autenticado e as pendencias dele (RN-060, RN-107).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPerfil()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(id, out var usuarioId))
            return Unauthorized();

        return Ok(await _service.ObterPerfilAsync(usuarioId));
    }

    /// <summary>
    /// Troca a senha do usuario autenticado. Encerra as demais sessoes: se a senha
    /// antiga vazou, o refresh dela nao pode continuar valendo.
    /// </summary>
    /// <remarks>
    /// E por aqui que o veterinario troca a senha temporaria recebida do Admin (P-05).
    /// </remarks>
    [HttpPost("trocar-senha")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> TrocarSenha([FromBody] TrocarSenhaDto dto)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(id, out var usuarioId))
            return Unauthorized();

        await _service.TrocarSenhaAsync(usuarioId, dto);
        return NoContent();
    }

    /// <summary>
    /// Emite um JWT sem senha, para desenvolvimento. Substituido por
    /// <c>POST /api/auth/login</c>, que autentica de verdade.
    /// </summary>
    /// <remarks>
    /// Fora do ambiente de Desenvolvimento a rota responde 404: emitir token sem
    /// credencial em producao seria uma porta aberta.
    /// </remarks>
    [HttpPost("token")]
    [AllowAnonymous]
    [Obsolete("Rota de desenvolvimento. Use POST /api/auth/login.")]
    [ProducesResponseType(typeof(TokenEmitidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GerarTokenDeDesenvolvimento([FromBody] TokenRequestDto request)
    {
        if (!_ambiente.IsDevelopment())
            return NotFound();

        if (request.Role != "Admin" && request.Role != "Veterinario")
            return BadRequest(new { erro = "Role invalida. Use 'Admin' ou 'Veterinario'." });

        var tipo = request.Role == "Admin" ? TipoUsuario.Admin : TipoUsuario.Veterinario;
        var acesso = _gerador.Emitir(Guid.NewGuid(), request.Usuario, request.Role, tipo);

        return Ok(new TokenEmitidoDto
        {
            Token = acesso.Token,
            ExpiraEm = acesso.ExpiraEm,
            Role = request.Role
        });
    }
}

/// <summary>Corpo da rota de token de desenvolvimento.</summary>
public sealed class TokenRequestDto
{
    public string Usuario { get; set; } = "usuario-teste";
    public string Role { get; set; } = "Admin";
}
