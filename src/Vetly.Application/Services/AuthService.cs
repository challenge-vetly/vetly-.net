using System.Security.Claims;
using Vetly.Application.DTOs.Auth;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de autenticação e sessão (§2.2, §3.1).
///
/// Atende as duas personas com credencial própria: o Responsável, que se cadastra
/// pelo app, e o veterinário, cuja senha é gerada pelo Admin no cadastro (P-05).
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>Role do Responsável.</summary>
    public const string RoleDoTutor = "Tutor";

    /// <summary>Role do veterinário ativo.</summary>
    public const string RoleDoVeterinario = "Veterinario";

    /// <summary>
    /// Role do veterinário desativado. Mantém o acesso apenas ao extrato dos próprios
    /// atendimentos, sem dados pessoais do Responsável ou do animal (RN-022/RN-024).
    /// </summary>
    public const string RoleDoVetDesativado = "VetDesativado";

    /// <summary>Validade do refresh token. Renovado a cada uso, por rotação.</summary>
    private static readonly TimeSpan ValidadeDoRefreshToken = TimeSpan.FromDays(30);

    private readonly ITutorRepository _tutorRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly ISenhaHasher _hasher;
    private readonly IGeradorDeTokenJwt _gerador;

    public AuthService(
        ITutorRepository tutorRepo,
        IVeterinarioRepository vetRepo,
        IRefreshTokenRepository refreshRepo,
        ISenhaHasher hasher,
        IGeradorDeTokenJwt gerador)
    {
        _tutorRepo = tutorRepo;
        _vetRepo = vetRepo;
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
        return await EmitirSessaoDeTutorAsync(tutor);
    }

    /// <inheritdoc/>
    public async Task<TokenEmitidoDto> LoginAsync(LoginDto dto)
    {
        // Responsavel primeiro, veterinario depois. As duas trilhas devolvem exatamente
        // o mesmo erro: distinguir entregaria a um atacante a lista de contas existentes.
        var tutor = await _tutorRepo.ObterPorEmailAsync(dto.Email);

        if (tutor is not null)
        {
            if (!tutor.Ativo || !tutor.TemCredencial() || !_hasher.Confere(dto.Senha, tutor.SenhaHash!))
                throw CredenciaisInvalidas();

            return await EmitirSessaoDeTutorAsync(tutor);
        }

        var vet = await _vetRepo.ObterPorEmailAsync(dto.Email);

        // Vet inativo NAO e barrado aqui: ele entra com a role VetDesativado, que so
        // alcanca o extrato dos proprios atendimentos (RN-024).
        if (vet is null || !vet.TemCredencial() || !_hasher.Confere(dto.Senha, vet.SenhaHash!))
            throw CredenciaisInvalidas();

        return await EmitirSessaoDeVeterinarioAsync(vet);
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

        return token.TipoUsuario switch
        {
            TipoUsuario.Tutor => await RenovarSessaoDeTutorAsync(token),
            TipoUsuario.Veterinario => await RenovarSessaoDeVeterinarioAsync(token),
            _ => throw new BusinessRuleException("AUTH-003", "Tipo de usuario nao suportado.")
        };
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
        var tutor = await _tutorRepo.ObterPorIdAsync(usuarioId);

        if (tutor is not null)
        {
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

        var vet = await _vetRepo.ObterPorIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario", usuarioId);

        var pendenciasDoVet = new List<string>();

        if (vet.SenhaTemporaria)
            pendenciasDoVet.Add("SenhaTemporaria");

        if (vet.CrmvStatus != StatusCrmv.Valido)
            pendenciasDoVet.Add("CrmvNaoValidado");

        if (!vet.Publicado && vet.Ativo)
            pendenciasDoVet.Add("PerfilNaoPublicado");

        if (vet.Endereco is null)
            pendenciasDoVet.Add("EnderecoNaoInformado");

        return new PerfilDto
        {
            Id = vet.Id,
            Nome = vet.Nome,
            Email = vet.Email ?? string.Empty,
            Role = RoleDeVeterinario(vet),
            TipoUsuario = TipoUsuario.Veterinario,
            Pendencias = pendenciasDoVet
        };
    }

    /// <inheritdoc/>
    public async Task TrocarSenhaAsync(Guid usuarioId, TrocarSenhaDto dto)
    {
        var tutor = await _tutorRepo.ObterPorIdAsync(usuarioId);

        if (tutor is not null)
        {
            if (!tutor.TemCredencial() || !_hasher.Confere(dto.SenhaAtual, tutor.SenhaHash!))
                throw new BusinessRuleException("AUTH-005", "Senha atual incorreta.");

            tutor.DefinirSenhaHash(_hasher.GerarHash(dto.NovaSenha));
            _tutorRepo.Atualizar(tutor);
            await _tutorRepo.SalvarAsync();
        }
        else
        {
            var vet = await _vetRepo.ObterPorIdAsync(usuarioId)
                ?? throw new NotFoundException("Usuario", usuarioId);

            if (!vet.TemCredencial() || !_hasher.Confere(dto.SenhaAtual, vet.SenhaHash!))
                throw new BusinessRuleException("AUTH-005", "Senha atual incorreta.");

            // Trocar a senha tira a marca de temporaria — e o que fecha a P-05
            vet.DefinirSenhaHash(_hasher.GerarHash(dto.NovaSenha), temporaria: false);
            _vetRepo.Atualizar(vet);
            await _vetRepo.SalvarAsync();
        }

        // Trocar a senha derruba as demais sessoes: se a antiga vazou, o refresh dela
        // nao pode continuar valendo.
        await _refreshRepo.RevogarTodosDoUsuarioAsync(usuarioId, DateTime.UtcNow);
        await _refreshRepo.SalvarAsync();
    }

    /// <summary>Erro único de credencial, para não revelar quais contas existem.</summary>
    private static BusinessRuleException CredenciaisInvalidas() =>
        new("AUTH-001", "E-mail ou senha invalidos.");

    /// <summary>
    /// Role do veterinário: desativado mantém acesso só ao extrato dos próprios
    /// atendimentos (RN-022/RN-024).
    /// </summary>
    private static string RoleDeVeterinario(Veterinario vet) =>
        vet.Ativo ? RoleDoVeterinario : RoleDoVetDesativado;

    private async Task<TokenEmitidoDto> RenovarSessaoDeTutorAsync(RefreshToken token)
    {
        var tutor = await _tutorRepo.ObterPorIdAsync(token.UsuarioId)
            ?? throw new BusinessRuleException("AUTH-002", "Refresh token invalido.");

        if (!tutor.Ativo)
            throw new BusinessRuleException("AUTH-004", "Cadastro inativo.");

        return await EmitirSessaoDeTutorAsync(tutor, token);
    }

    private async Task<TokenEmitidoDto> RenovarSessaoDeVeterinarioAsync(RefreshToken token)
    {
        var vet = await _vetRepo.ObterPorIdAsync(token.UsuarioId)
            ?? throw new BusinessRuleException("AUTH-002", "Refresh token invalido.");

        // Vet desativado renova, mas com a role reduzida — a desativacao rebaixa o
        // acesso na proxima renovacao, sem depender do token antigo expirar (RN-022).
        return await EmitirSessaoDeVeterinarioAsync(vet, token);
    }

    private async Task<TokenEmitidoDto> EmitirSessaoDeTutorAsync(Tutor tutor, RefreshToken? tokenRotacionado = null)
    {
        // A claim tutorId e o que permite ao serviço validar posse por linha (RN-105/106)
        var acesso = _gerador.Emitir(
            tutor.Id, tutor.Nome, RoleDoTutor, TipoUsuario.Tutor,
            [new Claim("tutorId", tutor.Id.ToString())]);

        var refreshToken = await PersistirRefreshAsync(tutor.Id, TipoUsuario.Tutor, tokenRotacionado);

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

    private async Task<TokenEmitidoDto> EmitirSessaoDeVeterinarioAsync(
        Veterinario vet, RefreshToken? tokenRotacionado = null)
    {
        var role = RoleDeVeterinario(vet);

        var claims = new List<Claim>
        {
            new("veterinarioId", vet.Id.ToString()),
            new("persona", vet.Persona.ToString()),
            new("plano", vet.Plano.ToString())
        };

        if (vet.EmpresaId is { } empresaId)
            claims.Add(new Claim("empresaId", empresaId.ToString()));

        var acesso = _gerador.Emitir(vet.Id, vet.Nome, role, TipoUsuario.Veterinario, claims);
        var refreshToken = await PersistirRefreshAsync(vet.Id, TipoUsuario.Veterinario, tokenRotacionado);

        return new TokenEmitidoDto
        {
            Token = acesso.Token,
            RefreshToken = refreshToken,
            ExpiraEm = acesso.ExpiraEm,
            Role = role,
            VeterinarioId = vet.Id,
            SenhaTemporaria = vet.SenhaTemporaria
        };
    }

    /// <summary>
    /// Persiste o novo refresh token e, quando a emissão vem de uma renovação, revoga
    /// o anterior apontando para o novo (rotação).
    /// </summary>
    private async Task<string> PersistirRefreshAsync(
        Guid usuarioId, TipoUsuario tipo, RefreshToken? tokenRotacionado)
    {
        var (refreshToken, hash) = _gerador.GerarRefreshToken();
        var novoRefresh = new RefreshToken(usuarioId, tipo, hash, DateTime.UtcNow.Add(ValidadeDoRefreshToken));

        await _refreshRepo.AdicionarAsync(novoRefresh);

        if (tokenRotacionado is not null)
        {
            tokenRotacionado.Revogar(DateTime.UtcNow, novoRefresh.Id);
            _refreshRepo.Atualizar(tokenRotacionado);
        }

        await _refreshRepo.SalvarAsync();
        return refreshToken;
    }
}
