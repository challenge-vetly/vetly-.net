using Moq;
using Vetly.Application.DTOs.Colmeia;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Colmeia: o historico do animal atravessando clinicas, sob autorizacao do
/// Responsavel (RN-090/RN-105).
/// </summary>
public class ColmeiaTests
{
    private readonly Mock<IColmeiaRepository> _repo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly Animal _animal;
    private readonly Veterinario _vet;

    public ColmeiaTests()
    {
        _animal = new Animal("Thor", "Canino", "SRD", new DateTime(2022, 3, 1), _tutorId);

        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);

        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(_animal);
        _vetRepo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<AcessoColmeia>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AdicionarLogAsync(It.IsAny<LogAcessoColmeia>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repo.Setup(r => r.ObterVigenteAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ReturnsAsync((AcessoColmeia?)null);
    }

    private ColmeiaService CriarServico() =>
        new(_repo.Object, _animalRepo.Object, _vetRepo.Object, _usuario.Object);

    private ConcederAcessoDto Pedido(
        EscopoAcessoColmeia escopo = EscopoAcessoColmeia.HistoricoCompleto, int? dias = null) => new()
    {
        AnimalId = _animal.Id,
        VeterinarioId = _vet.Id,
        Escopo = escopo,
        ValidadeEmDias = dias,
        Motivo = "Segunda opiniao"
    };

    // ── Concessão (RN-090) ───────────────────────────────────────────────────

    [Fact]
    public async Task Conceder_PeloResponsavel_AutorizaOVeterinario()
    {
        var acesso = await CriarServico().ConcederAsync(Pedido());

        Assert.True(acesso.Vigente);
        Assert.Equal(_vet.Id, acesso.VeterinarioId);
        Assert.Equal(EscopoAcessoColmeia.HistoricoCompleto, acesso.Escopo);
    }

    [Fact]
    public async Task Conceder_SemPrazoEscolhido_UsaOPadraoDe30Dias()
    {
        var acesso = await CriarServico().ConcederAsync(Pedido());

        // Acesso clinico que nao expira sozinho e acesso que ninguem lembra de revogar
        var duracao = acesso.ExpiraEm - acesso.ConcedidoEm;
        Assert.Equal(30, Math.Round(duracao.TotalDays));
    }

    [Fact]
    public async Task Conceder_PorOutroQueNaoOResponsavel_ERecusado()
    {
        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.EhVeterinario).Returns(true);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);

        // A clinica que quisesse se autoconceder acesso e o que esta guarda impede
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ConcederAsync(Pedido()));

        Assert.Equal("RN-105", ex.Codigo);
    }

    [Fact]
    public async Task Conceder_AVeterinarioDesativado_NaoEPermitido()
    {
        _vet.Desativar();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ConcederAsync(Pedido()));

        Assert.Equal("RN-090", ex.Codigo);
    }

    [Fact]
    public async Task Conceder_ComAutorizacaoVigente_Retorna409()
    {
        var vigente = new AcessoColmeia(
            _animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.HistoricoCompleto);

        _repo.Setup(r => r.ObterVigenteAsync(_animal.Id, _vet.Id, It.IsAny<DateTime>()))
            .ReturnsAsync(vigente);

        // Renovar em silencio deixaria o Responsavel com duas autorizacoes sem saber
        // qual vale
        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().ConcederAsync(Pedido()));

        Assert.Equal("RN-090", ex.Codigo);
    }

    // ── Revogação (RN-062/RN-090) ────────────────────────────────────────────

    [Fact]
    public async Task Revogar_EncerraAAutorizacao()
    {
        var acesso = new AcessoColmeia(_animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.Documentos);
        _repo.Setup(r => r.ObterPorIdAsync(acesso.Id)).ReturnsAsync(acesso);

        var resultado = await CriarServico().RevogarAsync(acesso.Id);

        Assert.NotNull(resultado.RevogadoEm);
        Assert.False(resultado.Vigente);
    }

    [Fact]
    public async Task Revogar_PorQuemNaoConcedeu_ERecusado()
    {
        var acesso = new AcessoColmeia(_animal.Id, Guid.NewGuid(), _vet.Id, EscopoAcessoColmeia.Documentos);
        _repo.Setup(r => r.ObterPorIdAsync(acesso.Id)).ReturnsAsync(acesso);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().RevogarAsync(acesso.Id));
    }

    [Fact]
    public async Task Revogar_DuasVezes_PreservaAPrimeiraData()
    {
        var acesso = new AcessoColmeia(_animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.Documentos);
        _repo.Setup(r => r.ObterPorIdAsync(acesso.Id)).ReturnsAsync(acesso);

        var servico = CriarServico();
        var primeira = await servico.RevogarAsync(acesso.Id);
        var segunda = await servico.RevogarAsync(acesso.Id);

        Assert.Equal(primeira.RevogadoEm, segunda.RevogadoEm);
    }

    // ── Vigência e escopo ────────────────────────────────────────────────────

    [Fact]
    public void Acesso_Revogado_DeixaDeValer()
    {
        var acesso = new AcessoColmeia(_animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.HistoricoCompleto);

        acesso.Revogar();

        Assert.False(acesso.Vigente(DateTime.UtcNow));
    }

    [Fact]
    public void Acesso_Vencido_DeixaDeValerSozinho()
    {
        var acesso = new AcessoColmeia(
            _animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.HistoricoCompleto, TimeSpan.FromDays(1));

        Assert.True(acesso.Vigente(DateTime.UtcNow));
        Assert.False(acesso.Vigente(DateTime.UtcNow.AddDays(2)));
    }

    [Fact]
    public void Acesso_ComEscopoAmplo_AlcancaOsMaisEstreitos()
    {
        var completo = new AcessoColmeia(
            _animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.HistoricoCompleto);

        Assert.True(completo.Alcanca(EscopoAcessoColmeia.Documentos));
        Assert.True(completo.Alcanca(EscopoAcessoColmeia.UltimaConsulta));
    }

    [Fact]
    public void Acesso_ComEscopoEstreito_NaoAlcancaOHistoricoInteiro()
    {
        var so_documentos = new AcessoColmeia(
            _animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.Documentos);

        // Pedir segunda opiniao sobre um exame nao e abrir o prontuario desde filhote
        Assert.False(so_documentos.Alcanca(EscopoAcessoColmeia.HistoricoCompleto));
        Assert.True(so_documentos.Alcanca(EscopoAcessoColmeia.Documentos));
    }

    [Fact]
    public void Acesso_ComValidadeAlemDoMaximo_NaoEAceito()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AcessoColmeia(
            _animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.Documentos, TimeSpan.FromDays(400)));
    }

    // ── Registro de acesso (RN-090) ──────────────────────────────────────────

    [Fact]
    public async Task Acesso_Negado_TambemFicaRegistrado()
    {
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);

        LogAcessoColmeia? registrado = null;
        _repo.Setup(r => r.AdicionarLogAsync(It.IsAny<LogAcessoColmeia>()))
            .Callback<LogAcessoColmeia>(l => registrado = l).Returns(Task.CompletedTask);

        await CriarServico().RegistrarAcessoAsync(
            _animal.Id, EscopoAcessoColmeia.HistoricoCompleto, permitido: false, "AnimalService");

        // Tentativa negada e justamente o que se quer enxergar numa auditoria
        Assert.NotNull(registrado);
        Assert.False(registrado.Permitido);
        Assert.Equal(_vet.Id, registrado.VeterinarioId);
    }

    [Fact]
    public async Task PodeAcessar_SemAutorizacao_EFalso()
    {
        var pode = await CriarServico().PodeAcessarAsync(
            _vet.Id, _animal.Id, EscopoAcessoColmeia.HistoricoCompleto);

        Assert.False(pode);
    }

    [Fact]
    public async Task PodeAcessar_ComAutorizacaoDeEscopoMenor_EFalso()
    {
        _repo.Setup(r => r.ObterVigenteAsync(_animal.Id, _vet.Id, It.IsAny<DateTime>()))
            .ReturnsAsync(new AcessoColmeia(_animal.Id, _tutorId, _vet.Id, EscopoAcessoColmeia.Documentos));

        var pode = await CriarServico().PodeAcessarAsync(
            _vet.Id, _animal.Id, EscopoAcessoColmeia.HistoricoCompleto);

        Assert.False(pode);
    }

    // ── Transparência para o Responsável ─────────────────────────────────────

    [Fact]
    public async Task Log_DoProprioAnimal_EVisivelAoResponsavel()
    {
        _repo.Setup(r => r.ObterLogDoAnimalAsync(_animal.Id)).ReturnsAsync(
        [
            new LogAcessoColmeia(_animal.Id, _vet.Id, EscopoAcessoColmeia.Documentos, true),
            new LogAcessoColmeia(_animal.Id, Guid.NewGuid(), EscopoAcessoColmeia.HistoricoCompleto, false)
        ]);

        var log = await CriarServico().ObterLogDoAnimalAsync(_animal.Id);

        // E a contrapartida da autorizacao: o Responsavel ve quem leu o que
        Assert.Equal(2, log.Count());
    }

    [Fact]
    public async Task Log_DeAnimalAlheio_ERecusado()
    {
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterLogDoAnimalAsync(_animal.Id));
    }

    // ── RN-064/RN-090: abertura automatica e prorrogacao ───────────────────

    [Fact]
    public void Prorrogar_SoAdia_NuncaEncurta()
    {
        var acesso = new AcessoColmeia(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            EscopoAcessoColmeia.HistoricoCompleto, TimeSpan.FromDays(30));

        var original = acesso.ExpiraEm;

        acesso.Prorrogar(DateTime.UtcNow.AddDays(5));

        // Encurtar por engano tiraria acesso que o Responsavel concedeu
        Assert.Equal(original, acesso.ExpiraEm);
    }

    [Fact]
    public void Prorrogar_RespeitaOTetoDeUmAno()
    {
        var acesso = new AcessoColmeia(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            EscopoAcessoColmeia.HistoricoCompleto, TimeSpan.FromDays(30));

        acesso.Prorrogar(DateTime.UtcNow.AddYears(5));

        // Autorizacao sem prazo e procuracao em branco
        Assert.True(acesso.ExpiraEm <= acesso.ConcedidoEm.Add(AcessoColmeia.ValidadeMaxima));
    }

    [Fact]
    public void Prorrogar_NaoRessuscitaAutorizacaoRevogada()
    {
        var acesso = new AcessoColmeia(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            EscopoAcessoColmeia.HistoricoCompleto, TimeSpan.FromDays(30));

        acesso.Revogar();
        var expiraQuandoRevogado = acesso.ExpiraEm;

        acesso.Prorrogar(DateTime.UtcNow.AddDays(90));

        // Revogar e a decisao mais explicita que o Responsavel pode tomar
        Assert.Equal(expiraQuandoRevogado, acesso.ExpiraEm);
        Assert.False(acesso.Vigente(DateTime.UtcNow));
    }
}
