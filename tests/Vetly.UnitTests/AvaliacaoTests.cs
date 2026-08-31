using Moq;
using Vetly.Application.DTOs.Avaliacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Avaliacao do atendimento e a reputacao que sai dela (RN-055/RN-057).
/// </summary>
public class AvaliacaoTests
{
    private readonly Mock<IAvaliacaoRepository> _repo = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly Veterinario _vet;

    public AvaliacaoTests()
    {
        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);

        _vetRepo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);
        _vetRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<Avaliacao>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repo.Setup(r => r.ObterDaConsultaAsync(It.IsAny<Guid>())).ReturnsAsync((Avaliacao?)null);
        _repo.Setup(r => r.ObterDoVeterinarioAsync(It.IsAny<Guid>())).ReturnsAsync([]);
    }

    private AvaliacaoService CriarServico() =>
        new(_repo.Object, _consultaRepo.Object, _vetRepo.Object, _usuario.Object);

    private Consulta Atendimento(bool realizada = true, int diasAtras = 1)
    {
        var consulta = Consulta.ParaCheckout(
            DateTime.UtcNow.AddDays(-diasAtras), _vet.Id, Guid.NewGuid(), _tutorId,
            Guid.NewGuid(), Guid.NewGuid());

        consulta.ConfirmarPagamento();

        if (realizada)
        {
            consulta.Finalizar();
            consulta.RegistrarEncerramento(DateTime.UtcNow.AddDays(-diasAtras));
        }

        _consultaRepo.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);

        return consulta;
    }

    private Avaliacao Nota(int nota, Guid? consultaId = null) =>
        new(consultaId ?? Guid.NewGuid(), _tutorId, _vet.Id, nota);

    // ── Quem pode avaliar (RN-055) ───────────────────────────────────────────

    [Fact]
    public async Task Avaliar_ConsultaRealizada_RegistraANota()
    {
        var consulta = Atendimento();

        var avaliacao = await CriarServico().AvaliarAsync(
            consulta.Id, new CriarAvaliacaoDto { Nota = 5, Comentario = "Atendimento excelente." });

        Assert.Equal(5, avaliacao.Nota);
        Assert.Equal("Atendimento excelente.", avaliacao.Comentario);
        Assert.Equal(_vet.Id, avaliacao.VeterinarioId);
    }

    [Fact]
    public async Task Avaliar_ConsultaNaoRealizada_NaoEPermitido()
    {
        var consulta = Atendimento(realizada: false);

        // Sem o vinculo com um atendimento que aconteceu, a nota vira numero que
        // qualquer um pode empurrar
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().AvaliarAsync(consulta.Id, new CriarAvaliacaoDto { Nota = 1 }));

        Assert.Equal("RN-055", ex.Codigo);
    }

    [Fact]
    public async Task Avaliar_ConsultaDeOutroResponsavel_ERecusado()
    {
        var consulta = Atendimento();
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().AvaliarAsync(consulta.Id, new CriarAvaliacaoDto { Nota = 5 }));

        Assert.Equal("RN-105", ex.Codigo);
    }

    [Fact]
    public async Task Avaliar_DuasVezes_Retorna409()
    {
        var consulta = Atendimento();
        _repo.Setup(r => r.ObterDaConsultaAsync(consulta.Id)).ReturnsAsync(Nota(4, consulta.Id));

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().AvaliarAsync(consulta.Id, new CriarAvaliacaoDto { Nota = 5 }));

        Assert.Equal("RN-055", ex.Codigo);
    }

    [Fact]
    public async Task Avaliar_ForaDoPrazo_NaoEPermitido()
    {
        var consulta = Atendimento(diasAtras: 45);

        // Avaliacao muito posterior mede memoria, nao atendimento
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().AvaliarAsync(consulta.Id, new CriarAvaliacaoDto { Nota = 5 }));

        Assert.Contains("prazo", ex.Message);
    }

    [Fact]
    public void Avaliacao_ComNotaForaDaEscala_NaoEAceita()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Avaliacao(Guid.NewGuid(), _tutorId, _vet.Id, 6));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Avaliacao(Guid.NewGuid(), _tutorId, _vet.Id, 0));
    }

    // ── Reputação (RN-057) ───────────────────────────────────────────────────

    [Fact]
    public async Task Reputacao_ERecalculadaAPartirDasAvaliacoes()
    {
        var consulta = Atendimento();
        _repo.Setup(r => r.ObterDoVeterinarioAsync(_vet.Id)).ReturnsAsync(
            [Nota(5), Nota(4), Nota(3)]);

        await CriarServico().AvaliarAsync(consulta.Id, new CriarAvaliacaoDto { Nota = 4 });

        // Recalcular, e nao incrementar: media acumulada em campo diverge do que esta
        // gravado assim que uma avaliacao e moderada ou corrigida
        Assert.Equal(4.00m, _vet.NotaMedia);
        Assert.Equal(3, _vet.NumAvaliacoes);
    }

    [Fact]
    public async Task Reputacao_ComMenosDeTresAvaliacoes_NaoEPublica()
    {
        _repo.Setup(r => r.ObterDoVeterinarioAsync(_vet.Id)).ReturnsAsync([Nota(5), Nota(5)]);
        _vet.AtualizarReputacao(5m, 2);

        var reputacao = await CriarServico().ObterReputacaoAsync(_vet.Id);

        // Uma nota 5 vinda de poucas avaliacoes nao diz nada sobre o profissional
        Assert.False(reputacao.NotaPublica);
        Assert.Equal(3, reputacao.MinimoParaNotaPublica);
    }

    [Fact]
    public async Task Reputacao_ComTresAvaliacoes_PassaAValerPublicamente()
    {
        _repo.Setup(r => r.ObterDoVeterinarioAsync(_vet.Id)).ReturnsAsync([Nota(5), Nota(4), Nota(5)]);
        _vet.AtualizarReputacao(4.67m, 3);

        var reputacao = await CriarServico().ObterReputacaoAsync(_vet.Id);

        Assert.True(reputacao.NotaPublica);
        Assert.Equal(4.67m, reputacao.NotaMedia);
    }

    [Fact]
    public async Task Reputacao_TrazADistribuicaoDasNotas()
    {
        _repo.Setup(r => r.ObterDoVeterinarioAsync(_vet.Id)).ReturnsAsync(
            [Nota(5), Nota(5), Nota(3), Nota(1)]);

        var reputacao = await CriarServico().ObterReputacaoAsync(_vet.Id);

        // Media de 3,5 com metade das notas em 5 e outra em 1 e uma historia diferente
        // de media 3,5 com todas em 3 e 4
        Assert.Equal(2, reputacao.Distribuicao[5]);
        Assert.Equal(1, reputacao.Distribuicao[3]);
        Assert.Equal(1, reputacao.Distribuicao[1]);
        Assert.Equal(0, reputacao.Distribuicao[2]);
    }

    // ── Resposta do veterinário (RN-055) ─────────────────────────────────────

    [Fact]
    public async Task Resposta_DoVeterinarioAvaliado_EAceita()
    {
        var avaliacao = Nota(2);
        _repo.Setup(r => r.ObterPorIdAsync(avaliacao.Id)).ReturnsAsync(avaliacao);

        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);

        var resultado = await CriarServico().ResponderAsync(
            avaliacao.Id, new ResponderAvaliacaoDto { Resposta = "Obrigada pelo retorno." });

        Assert.Equal("Obrigada pelo retorno.", resultado.RespostaDoVeterinario);
        Assert.NotNull(resultado.RespondidaEm);
    }

    [Fact]
    public async Task Resposta_DeOutroVeterinario_ERecusada()
    {
        var avaliacao = Nota(2);
        _repo.Setup(r => r.ObterPorIdAsync(avaliacao.Id)).ReturnsAsync(avaliacao);

        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(Guid.NewGuid());

        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().ResponderAsync(
            avaliacao.Id, new ResponderAvaliacaoDto { Resposta = "resposta" }));
    }

    [Fact]
    public async Task Resposta_DuasVezes_Retorna409()
    {
        var avaliacao = Nota(2);
        avaliacao.Responder("primeira resposta");
        _repo.Setup(r => r.ObterPorIdAsync(avaliacao.Id)).ReturnsAsync(avaliacao);

        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);

        // A avaliacao e do Responsavel, e a replica nao vira debate no perfil
        await Assert.ThrowsAsync<ConflitoDeEstadoException>(() => CriarServico().ResponderAsync(
            avaliacao.Id, new ResponderAvaliacaoDto { Resposta = "segunda" }));
    }

    // ── Moderação ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Moderacao_EscondeOComentarioEPreservaANota()
    {
        var avaliacao = new Avaliacao(Guid.NewGuid(), _tutorId, _vet.Id, 1, "texto ofensivo");
        _repo.Setup(r => r.ObterPorIdAsync(avaliacao.Id)).ReturnsAsync(avaliacao);

        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        var resultado = await CriarServico().ModerarAsync(
            avaliacao.Id, new ModerarAvaliacaoDto { Motivo = "Linguagem ofensiva" });

        // Esconder o texto nao pode virar um jeito de apagar uma avaliacao ruim
        Assert.Null(resultado.Comentario);
        Assert.True(resultado.ComentarioModerado);
        Assert.Equal(1, resultado.Nota);
    }

    [Fact]
    public async Task Moderacao_PorQuemNaoEAdmin_ERecusada()
    {
        var avaliacao = Nota(1);
        _repo.Setup(r => r.ObterPorIdAsync(avaliacao.Id)).ReturnsAsync(avaliacao);

        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().ModerarAsync(
            avaliacao.Id, new ModerarAvaliacaoDto { Motivo = "nao gostei" }));

        Assert.Equal("RN-106", ex.Codigo);
    }

    [Fact]
    public void Moderacao_SemMotivo_NaoEAceita()
    {
        var avaliacao = Nota(1);

        // Moderacao sem motivo nao se audita
        Assert.Throws<ArgumentException>(() => avaliacao.ModerarComentario("   "));
    }

    [Fact]
    public async Task Moderacao_NaoTiraANotaDaMedia()
    {
        var moderada = new Avaliacao(Guid.NewGuid(), _tutorId, _vet.Id, 1, "texto");
        moderada.ModerarComentario("Linguagem ofensiva");

        var consulta = Atendimento();
        _repo.Setup(r => r.ObterDoVeterinarioAsync(_vet.Id)).ReturnsAsync([moderada, Nota(5), Nota(3)]);

        await CriarServico().AvaliarAsync(consulta.Id, new CriarAvaliacaoDto { Nota = 3 });

        // (1 + 5 + 3) / 3 = 3,00 — a nota moderada continua contando
        Assert.Equal(3.00m, _vet.NotaMedia);
    }
}
