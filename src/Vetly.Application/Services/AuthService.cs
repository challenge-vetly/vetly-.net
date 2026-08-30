using System.Security.Claims;
using Vetly.Application.DTOs.Auth;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de autenticação e sessão do Responsável (§2.2, §3.1).
///
/// Nesta onda apenas o Tutor tem credencial própria; o veterinário entra na branch
/// seguinte, junto da senha temporária devolvida ao Admin (pendência P-05).
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>Role usada no token do Responsável.</summary>
    public const string RoleDoTutor = "Tutor";

    /// <summary>Validade do refresh token. Renovado a cada uso, por rotação.</summary>
    private static readonly TimeSpan ValidadeDoRefreshToken = TimeSpan.FromDays(30);

    private readonly ITutorRepository _tutorRepo;
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly ISenhaHasher _hasher;
    private readonly IGeradorDeTokenJwt _gerador;

    public AuthService(
        ITutorRepository tutorRepo,
        IRefreshTokenRepository refreshRepo,
        ISenhaHasher hasher,
        IGeradorDeTokenJwt gerador)
    {
        _tutorRepo = tutorRepo;
        _refreshRepo = refreshRepo;
        _hasher = hasher;
        _gerador = gerador;
    }

    /// <inheritdoc/>
    public async Task<TokenEmitidoDto> RegistrarTutorAsync(RegistrarTutorDto dto)
    {
        var existente = await _tutorRepo.ObterPorEmailAsync(dto.Email);
        if (existente is not null)
            throw new BusinessRuleException("TUTOR-001", "E-mail ja cadastrado na plataforma.");

        var tutor = new Tutor(dto.Nome, dto.Email, dto.Telefone);
        tutor.DefinirSenhaHash(_hasher.GerarHash(dto.Senha));

        await _tutorRepo.AdicionarAsync(tutor);
        await _tutorRepo.SalvarAsync();

        // Nasce sem consentimento: a base legal precede o tratamento de dados (RN-060).
        // O token ja e emitido para o app conseguir chamar a rota de consentimento.
        return await EmitirSessaoAsync(tutor);
    }

    /// <inheritdoc/>
    public async Task<TokenEmitidoDto> LoginAsync(LoginDto dto)
    {
        var tutor = await _tutorRepo.ObterPorEmailAsync(dto.Email);

        // Mensagem unica para e-mail inexistente, senha errada ou conta sem credencial:
        // distinguir os casos entregaria a um atacante a lista de e-mails cadastrados.
        if (tutor is null || !tutor.Ativo || !tutor.TemCredencial() ||
            !_hasher.Confere(dto.Senha, tutor.SenhaHash!))
        {
            throw new BusinessRuleException("AUTH-001", "E-mail ou senha invalidos.");
        }

        return await EmitirSessaoAsync(tutor);
    }

    /// <inheritdoc/>
    public async Task<TokenEmitidoDto> RenovarAsync(RefreshDto dto)
    {
        var hash = _gerador.CalcularHash(dto.RefreshToken);
        var token = await _refreshRepo.ObterPorHashAsync(hash)
            ?? throw new BusinessRuleException("AUTH-002", "Refresh token invalido.");

        var agora = DateTime.UtcNow;

        if (!token.EstaValido(agora))
        {
            // Token ja revogado sendo reapresentado e sinal de vazamento: derruba a cadeia
            // inteira do usuario, nao apenas este token.
            await _refreshRepo.RevogarTodosDoUsuarioAsync(token.UsuarioId, agora);
            await _refreshRepo.SalvarAsync();

            throw new BusinessRuleException("AUTH-002", "Refresh token expirado ou ja utilizado.");
        }

        if (token.TipoUsuario != TipoUsuario.Tutor)
            throw new BusinessRuleException("AUTH-003", "Tipo de usuario nao suportado nesta versao.");

        var tutor = await _tutorRepo.ObterPorIdAsync(token.UsuarioId)
            ?? throw new BusinessRuleException("AUTH-002", "Refresh token invalido.");

        if (!tutor.Ativo)
            throw new BusinessRuleException("AUTH-004", "Cadastro inativo.");

        return await EmitirSessaoAsync(tutor, tokenRotacionado: token);
    }

    /// <inheritdoc/>
    public async Task EncerrarSessaoAsync(RefreshDto dto)
    {
        var hash = _gerador.CalcularHash(dto.RefreshToken);
        var token = await _refreshRepo.ObterPorHashAsync(hash);

        // Logout e idempotente: token desconhecido ou ja revogado nao e erro para o cliente
        if (token is null || token.Revogado)
            return;

        token.Revogar(DateTime.UtcNow);
        _refreshRepo.Atualizar(token);
        await _refreshRepo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<PerfilDto> ObterPerfilAsync(Guid usuarioId)
    {
        var tutor = await _tutorRepo.ObterPorIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario", usuarioId);

        var pendencias = new List<string>();

        if (!tutor.Consentiu(FinalidadeConsentimento.Atendimento))
            pendencias.Add("ConsentimentoAtendimento");

        if (!tutor.TemCredencial())
            pendencias.Add("SenhaNaoDefinida");

        return new PerfilDto
        {
            Id = tutor.Id,
            Nome = tutor.Nome,
            Email = tutor.Email,
            Role = RoleDoTutor,
            TipoUsuario = TipoUsuario.Tutor,
            Pendencias = pendencias
        };
    }

    /// <summary>
    /// Emite o par de tokens e persiste o refresh. Quando vem de uma renovação,
    /// revoga o token anterior apontando para o novo (rotação).
    /// </summary>
    private async Task<TokenEmitidoDto> EmitirSessaoAsync(Tutor tutor, RefreshToken? tokenRotacionado = null)
    {
        // A claim tutorId e o que permite ao serviço validar posse por linha (RN-105/RN-106)
        var acesso = _gerador.Emitir(
            tutor.Id, tutor.Nome, RoleDoTutor, TipoUsuario.Tutor,
            [new Claim("tutorId", tutor.Id.ToString())]);

        var (refreshToken, hash) = _gerador.GerarRefreshToken();
        var novoRefresh = new RefreshToken(
            tutor.Id, TipoUsuario.Tutor, hash, DateTime.UtcNow.Add(ValidadeDoRefreshToken));

        await _refreshRepo.AdicionarAsync(novoRefresh);

        if (tokenRotacionado is not null)
        {
            tokenRotacionado.Revogar(DateTime.UtcNow, novoRefresh.Id);
            _refreshRepo.Atualizar(tokenRotacionado);
        }

        await _refreshRepo.SalvarAsync();

        return new TokenEmitidoDto
        {
            Token = acesso.Token,
            RefreshToken = refreshToken,
            ExpiraEm = acesso.ExpiraEm,
            Role = RoleDoTutor,
            TutorId = tutor.Id,
            ConsentimentoPendente = !tutor.Consentiu(FinalidadeConsentimento.Atendimento)
        };
    }
}
