using Moq;
using Vetly.Application.DTOs.Auth;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes do AuthService: cadastro, login, rotacao de refresh token e logout (§2.2, §3.1).
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<ITutorRepository> _tutorRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IRefreshTokenRepository> _refreshRepo = new();
    private readonly Mock<ISenhaHasher> _hasher = new();
    private readonly Mock<IGeradorDeTokenJwt> _gerador = new();

    public AuthServiceTests()
    {
        _hasher.Setup(h => h.GerarHash(It.IsAny<string>())).Returns("hash-da-senha");
        _gerador
            .Setup(g => g.Emitir(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<TipoUsuario>(), It.IsAny<IEnumerable<System.Security.Claims.Claim>>()))
            .Returns(new TokenDeAcesso("jwt-de-teste", DateTime.UtcNow.AddHours(8)));
        _gerador.Setup(g => g.GerarRefreshToken()).Returns(("refresh-em-claro", "hash-do-refresh"));
        _gerador.Setup(g => g.CalcularHash(It.IsAny<string>())).Returns("hash-do-refresh");

        _refreshRepo.Setup(r => r.AdicionarAsync(It.IsAny<RefreshToken>())).Returns(Task.CompletedTask);
        _refreshRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Veterinario?)null);
        _tutorRepo.Setup(r => r.AdicionarAsync(It.IsAny<Tutor>())).Returns(Task.CompletedTask);
        _tutorRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
    }

    private AuthService CriarServico() =>
        new(_tutorRepo.Object, _vetRepo.Object, _refreshRepo.Object, _hasher.Object, _gerador.Object);

    private static Tutor TutorComCredencial()
    {
        var tutor = new Tutor("Ana", "ana@exemplo.com", "11999998888");
        tutor.DefinirSenhaHash("hash-da-senha");
        return tutor;
    }

    // ── Cadastro (RN-060) ────────────────────────────────────────────────────

    [Fact]
    public async Task RegistrarTutorAsync_EmiteSessaoComConsentimentoPendente()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Tutor?)null);

        var resultado = await CriarServico().RegistrarTutorAsync(new RegistrarTutorDto
        {
            Nome = "Ana", Email = "ana@exemplo.com", Telefone = "11999998888", Senha = "senha-forte-123"
        });

        Assert.Equal("Tutor", resultado.Role);
        Assert.NotNull(resultado.TutorId);
        // Base legal precede o tratamento: o cadastro nasce sem consentimento (RN-060)
        Assert.True(resultado.ConsentimentoPendente);
    }

    [Fact]
    public async Task RegistrarTutorAsync_EmailJaCadastrado_LancaBusinessRuleException()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync(TutorComCredencial());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().RegistrarTutorAsync(new RegistrarTutorDto
            {
                Nome = "Ana", Email = "ana@exemplo.com", Telefone = "1199", Senha = "senha-forte-123"
            }));

        Assert.Equal("TUTOR-001", ex.Codigo);
    }

    [Fact]
    public async Task RegistrarTutorAsync_NuncaPersisteSenhaEmClaro()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Tutor?)null);
        Tutor? persistido = null;
        _tutorRepo.Setup(r => r.AdicionarAsync(It.IsAny<Tutor>()))
            .Callback<Tutor>(t => persistido = t)
            .Returns(Task.CompletedTask);

        await CriarServico().RegistrarTutorAsync(new RegistrarTutorDto
        {
            Nome = "Ana", Email = "ana@exemplo.com", Telefone = "1199", Senha = "senha-forte-123"
        });

        Assert.NotNull(persistido);
        Assert.Equal("hash-da-senha", persistido!.SenhaHash);
        Assert.DoesNotContain("senha-forte-123", persistido.SenhaHash!);
    }

    // ── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_CredenciaisCorretas_EmiteParDeTokens()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync("ana@exemplo.com")).ReturnsAsync(TutorComCredencial());
        _hasher.Setup(h => h.Confere("senha-certa", "hash-da-senha")).Returns(true);

        var resultado = await CriarServico().LoginAsync(new LoginDto
        {
            Email = "ana@exemplo.com", Senha = "senha-certa"
        });

        Assert.Equal("jwt-de-teste", resultado.Token);
        Assert.Equal("refresh-em-claro", resultado.RefreshToken);
    }

    [Fact]
    public async Task LoginAsync_SenhaErrada_LancaAuth001()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync("ana@exemplo.com")).ReturnsAsync(TutorComCredencial());
        _hasher.Setup(h => h.Confere(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().LoginAsync(new LoginDto { Email = "ana@exemplo.com", Senha = "errada" }));

        Assert.Equal("AUTH-001", ex.Codigo);
    }

    [Fact]
    public async Task LoginAsync_EmailInexistente_DaAMesmaRespostaDeSenhaErrada()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Tutor?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().LoginAsync(new LoginDto { Email = "ninguem@exemplo.com", Senha = "x" }));

        // Distinguir os casos entregaria a lista de e-mails cadastrados
        Assert.Equal("AUTH-001", ex.Codigo);
        Assert.Equal("E-mail ou senha invalidos.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_TutorInativo_NaoAutentica()
    {
        var tutor = TutorComCredencial();
        tutor.Desativar();
        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync(tutor);
        _hasher.Setup(h => h.Confere(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().LoginAsync(new LoginDto { Email = "ana@exemplo.com", Senha = "senha-certa" }));

        Assert.Equal("AUTH-001", ex.Codigo);
    }

    // ── Login do veterinário (P-05) e offboarding (RN-022/RN-024) ────────────

    private static Veterinario VetComCredencial(bool ativo = true)
    {
        var vet = new Veterinario("Dra. Marina", new Vetly.Domain.ValueObjects.Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        vet.DefinirEmail("marina@exemplo.com");
        vet.DefinirSenhaHash("hash-da-senha", temporaria: true);

        if (!ativo) vet.Desativar();
        return vet;
    }

    [Fact]
    public async Task LoginAsync_VeterinarioAtivo_RecebeRoleVeterinario()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Tutor?)null);
        _vetRepo.Setup(r => r.ObterPorEmailAsync("marina@exemplo.com")).ReturnsAsync(VetComCredencial());
        _hasher.Setup(h => h.Confere(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var resultado = await CriarServico().LoginAsync(new LoginDto
        {
            Email = "marina@exemplo.com", Senha = "SenhaTemp123"
        });

        Assert.Equal("Veterinario", resultado.Role);
        Assert.NotNull(resultado.VeterinarioId);
        Assert.True(resultado.SenhaTemporaria);
    }

    [Fact]
    public async Task LoginAsync_VeterinarioDesativado_EntraComRoleReduzida()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Tutor?)null);
        _vetRepo.Setup(r => r.ObterPorEmailAsync("marina@exemplo.com")).ReturnsAsync(VetComCredencial(ativo: false));
        _hasher.Setup(h => h.Confere(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var resultado = await CriarServico().LoginAsync(new LoginDto
        {
            Email = "marina@exemplo.com", Senha = "SenhaTemp123"
        });

        // Ele nao e barrado no login: precisa entrar para pedir o extrato dos
        // proprios atendimentos (RN-024). O que muda e a role.
        Assert.Equal("VetDesativado", resultado.Role);
    }

    [Fact]
    public async Task LoginAsync_VeterinarioSemCredencial_DaAMesmaRespostaDeSenhaErrada()
    {
        var semCredencial = new Veterinario("Dr. Antigo", new Vetly.Domain.ValueObjects.Crmv("99999-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Basico);

        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Tutor?)null);
        _vetRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync(semCredencial);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().LoginAsync(new LoginDto { Email = "antigo@exemplo.com", Senha = "x" }));

        Assert.Equal("AUTH-001", ex.Codigo);
    }

    [Fact]
    public async Task TrocarSenhaAsync_DerrubaAsDemaisSessoes()
    {
        var tutor = TutorComCredencial();
        _tutorRepo.Setup(r => r.ObterPorIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _tutorRepo.Setup(r => r.Atualizar(It.IsAny<Tutor>()));
        _hasher.Setup(h => h.Confere("senha-atual", It.IsAny<string>())).Returns(true);
        _refreshRepo.Setup(r => r.RevogarTodosDoUsuarioAsync(tutor.Id, It.IsAny<DateTime>())).ReturnsAsync(3);

        await CriarServico().TrocarSenhaAsync(tutor.Id, new TrocarSenhaDto
        {
            SenhaAtual = "senha-atual", NovaSenha = "nova-senha-forte-123"
        });

        // Se a senha antiga vazou, o refresh emitido com ela nao pode continuar valendo
        _refreshRepo.Verify(r => r.RevogarTodosDoUsuarioAsync(tutor.Id, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task TrocarSenhaAsync_SenhaAtualErrada_LancaAuth005()
    {
        var tutor = TutorComCredencial();
        _tutorRepo.Setup(r => r.ObterPorIdAsync(tutor.Id)).ReturnsAsync(tutor);
        _hasher.Setup(h => h.Confere(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().TrocarSenhaAsync(tutor.Id, new TrocarSenhaDto
            {
                SenhaAtual = "errada", NovaSenha = "nova-senha-forte-123"
            }));

        Assert.Equal("AUTH-005", ex.Codigo);
    }

    // ── Rotação do refresh token ─────────────────────────────────────────────

    [Fact]
    public async Task RenovarAsync_TokenValido_RotacionaRevogandoOAnterior()
    {
        var tutor = TutorComCredencial();
        var token = new RefreshToken(tutor.Id, TipoUsuario.Tutor, "hash-do-refresh", DateTime.UtcNow.AddDays(7));

        _refreshRepo.Setup(r => r.ObterPorHashAsync("hash-do-refresh")).ReturnsAsync(token);
        _tutorRepo.Setup(r => r.ObterPorIdAsync(tutor.Id)).ReturnsAsync(tutor);

        var resultado = await CriarServico().RenovarAsync(new RefreshDto { RefreshToken = "refresh-em-claro" });

        Assert.Equal("refresh-em-claro", resultado.RefreshToken);
        Assert.True(token.Revogado);
        // A rotacao aponta para o token que substituiu — e isso que torna reuso detectavel
        Assert.NotNull(token.SubstituidoPorId);
    }

    [Fact]
    public async Task RenovarAsync_TokenJaRevogado_DerrubaTodasAsSessoesDoUsuario()
    {
        var tutor = TutorComCredencial();
        var token = new RefreshToken(tutor.Id, TipoUsuario.Tutor, "hash-do-refresh", DateTime.UtcNow.AddDays(7));
        token.Revogar(DateTime.UtcNow.AddMinutes(-5));

        _refreshRepo.Setup(r => r.ObterPorHashAsync("hash-do-refresh")).ReturnsAsync(token);
        _refreshRepo.Setup(r => r.RevogarTodosDoUsuarioAsync(tutor.Id, It.IsAny<DateTime>())).ReturnsAsync(2);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().RenovarAsync(new RefreshDto { RefreshToken = "refresh-em-claro" }));

        Assert.Equal("AUTH-002", ex.Codigo);
        // Reapresentar token ja usado e sinal de vazamento: derruba a cadeia inteira
        _refreshRepo.Verify(r => r.RevogarTodosDoUsuarioAsync(tutor.Id, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task RenovarAsync_TokenExpirado_NaoRenova()
    {
        var tutor = TutorComCredencial();
        var token = new RefreshToken(tutor.Id, TipoUsuario.Tutor, "hash-do-refresh", DateTime.UtcNow.AddMinutes(-1));

        _refreshRepo.Setup(r => r.ObterPorHashAsync("hash-do-refresh")).ReturnsAsync(token);
        _refreshRepo.Setup(r => r.RevogarTodosDoUsuarioAsync(It.IsAny<Guid>(), It.IsAny<DateTime>())).ReturnsAsync(1);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().RenovarAsync(new RefreshDto { RefreshToken = "refresh-em-claro" }));

        Assert.Equal("AUTH-002", ex.Codigo);
    }

    [Fact]
    public async Task RenovarAsync_TokenDesconhecido_LancaAuth002()
    {
        _refreshRepo.Setup(r => r.ObterPorHashAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().RenovarAsync(new RefreshDto { RefreshToken = "qualquer-coisa" }));

        Assert.Equal("AUTH-002", ex.Codigo);
    }

    // ── Logout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task EncerrarSessaoAsync_RevogaOToken()
    {
        var token = new RefreshToken(Guid.NewGuid(), TipoUsuario.Tutor, "hash-do-refresh", DateTime.UtcNow.AddDays(7));
        _refreshRepo.Setup(r => r.ObterPorHashAsync("hash-do-refresh")).ReturnsAsync(token);
        _refreshRepo.Setup(r => r.Atualizar(It.IsAny<RefreshToken>()));

        await CriarServico().EncerrarSessaoAsync(new RefreshDto { RefreshToken = "refresh-em-claro" });

        Assert.True(token.Revogado);
    }

    [Fact]
    public async Task EncerrarSessaoAsync_TokenDesconhecido_EIdempotente()
    {
        _refreshRepo.Setup(r => r.ObterPorHashAsync(It.IsAny<string>())).ReturnsAsync((RefreshToken?)null);

        // Logout de token que nao existe nao e erro para o cliente
        await CriarServico().EncerrarSessaoAsync(new RefreshDto { RefreshToken = "sumiu" });

        _refreshRepo.Verify(r => r.SalvarAsync(), Times.Never);
    }

    // ── Perfil (RN-060) ──────────────────────────────────────────────────────

    [Fact]
    public async Task ObterPerfilAsync_SemConsentimento_ListaAPendencia()
    {
        var tutor = TutorComCredencial();
        _tutorRepo.Setup(r => r.ObterPorIdAsync(tutor.Id)).ReturnsAsync(tutor);

        var perfil = await CriarServico().ObterPerfilAsync(tutor.Id);

        Assert.Contains("ConsentimentoAtendimento", perfil.Pendencias);
    }

    [Fact]
    public async Task ObterPerfilAsync_ComConsentimento_NaoListaPendencia()
    {
        var tutor = TutorComCredencial();
        tutor.RegistrarConsentimento(FinalidadeConsentimento.Atendimento, true, DateTime.UtcNow);
        _tutorRepo.Setup(r => r.ObterPorIdAsync(tutor.Id)).ReturnsAsync(tutor);

        var perfil = await CriarServico().ObterPerfilAsync(tutor.Id);

        Assert.Empty(perfil.Pendencias);
    }

    // ── Paridade de mensagem no login ──────────────────────────────────────

    [Fact]
    public async Task Login_EmailInexistente_ESenhaErrada_DaoAMesmaResposta()
    {
        _tutorRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Tutor?)null);
        _vetRepo.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Veterinario?)null);

        var inexistente = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().LoginAsync(new LoginDto
            {
                Email = "ninguem@exemplo.com", Senha = "senha-forte-123"
            }));

        var tutor = new Tutor("Ana Souza", "ana@exemplo.com", "11999998888");
        tutor.DefinirSenhaHash("hash-que-nao-confere");

        _tutorRepo.Setup(r => r.ObterPorEmailAsync("ana@exemplo.com")).ReturnsAsync(tutor);
        _hasher.Setup(h => h.Confere(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var senhaErrada = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().LoginAsync(new LoginDto
            {
                Email = "ana@exemplo.com", Senha = "senha-errada"
            }));

        // Distinguir os dois casos entregaria a um atacante a lista de contas
        // existentes: a resposta tem de ser identica, codigo e texto
        Assert.Equal(inexistente.Codigo, senhaErrada.Codigo);
        Assert.Equal(inexistente.Message, senhaErrada.Message);
    }

    [Fact]
    public async Task Login_ResponsavelDesativado_DaAMesmaRespostaDeCredencialInvalida()
    {
        var tutor = new Tutor("Ana Souza", "ana@exemplo.com", "11999998888");
        tutor.DefinirSenhaHash("hash");
        tutor.Desativar();

        _tutorRepo.Setup(r => r.ObterPorEmailAsync("ana@exemplo.com")).ReturnsAsync(tutor);
        _hasher.Setup(h => h.Confere(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().LoginAsync(new LoginDto
            {
                Email = "ana@exemplo.com", Senha = "senha-forte-123"
            }));

        // Conta desativada tambem nao se anuncia: "esta conta existe mas foi
        // desativada" ja e informacao demais para quem esta tentando adivinhar
        Assert.Equal("AUTH-001", ex.Codigo);
        Assert.Equal("E-mail ou senha invalidos.", ex.Message);
    }
}
