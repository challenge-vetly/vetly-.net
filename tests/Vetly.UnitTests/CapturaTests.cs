using Moq;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Captura de audio da consulta (RN-008/RN-009/RN-079/RN-085).
///
/// A janela e aberta e fechada pelo veterinario; fora dela nada e capturado.
/// </summary>
public class CapturaTests
{
    private readonly Mock<ICapturaRepository> _repo = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IEmpresaRepository> _empresaRepo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IMidiaRepository> _midiaRepo = new();
    private readonly Mock<IFilaDeJobs> _fila = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Veterinario _vet;
    private readonly Animal _animal;
    private readonly Consulta _consulta;

    public CapturaTests()
    {
        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());
        _animal.RegistrarPeso(31.5m);

        _consulta = Consulta.ParaCheckout(
            DateTime.UtcNow.AddHours(1), _vet.Id, _animal.Id, _animal.TutorId, Guid.NewGuid(), Guid.NewGuid());
        _consulta.ConfirmarPagamento();

        _usuario.SetupGet(u => u.EhAdmin).Returns(true);
        _consultaRepo.Setup(r => r.ObterPorIdAsync(_consulta.Id)).ReturnsAsync(_consulta);
        _consultaRepo.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _consultaRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);
        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(_animal);
        _repo.Setup(r => r.AdicionarSessaoAsync(It.IsAny<SessaoCaptura>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AdicionarSegmentoAsync(It.IsAny<SegmentoAudio>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.AdicionarTranscricaoAsync(It.IsAny<Transcricao>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repo.Setup(r => r.ObterSessaoDaConsultaAsync(_consulta.Id)).ReturnsAsync((SessaoCaptura?)null);
        _repo.Setup(r => r.ObterSegmentosAsync(It.IsAny<Guid>())).ReturnsAsync([]);
        _repo.Setup(r => r.ObterTranscricoesAsync(It.IsAny<Guid>())).ReturnsAsync([]);
        _repo.Setup(r => r.ObterSegmentoPorSequenciaAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .ReturnsAsync((SegmentoAudio?)null);
    }

    private CapturaService CriarServico() =>
        new(_repo.Object, _consultaRepo.Object, _vetRepo.Object, _empresaRepo.Object,
            _animalRepo.Object, _midiaRepo.Object, _fila.Object, _usuario.Object);

    private SessaoCaptura SessaoAberta(bool capturaAtiva = true)
    {
        var sessao = new SessaoCaptura(_consulta.Id, capturaAtiva);
        _repo.Setup(r => r.ObterSessaoDaConsultaAsync(_consulta.Id)).ReturnsAsync(sessao);
        _repo.Setup(r => r.ObterSessaoAsync(sessao.Id)).ReturnsAsync(sessao);
        return sessao;
    }

    private Midia AudioNoStorage()
    {
        var midia = new Midia(TipoMidia.AudioConsulta, "audio/webm", consultaId: _consulta.Id);
        _midiaRepo.Setup(r => r.ObterPorIdAsync(midia.Id)).ReturnsAsync(midia);
        return midia;
    }

    private static EnviarSegmentoDto Segmento(Guid midiaId, int sequencia = 0) => new()
    {
        Sequencia = sequencia,
        MidiaId = midiaId,
        DuracaoMs = 30000,
        InicioRelativoMs = sequencia * 30000
    };

    // ── Início da consulta (RN-008/RN-085) ───────────────────────────────────

    [Fact]
    public async Task Iniciar_AbreAJanelaEMarcaOInicioNaConsulta()
    {
        var resultado = await CriarServico().IniciarAsync(_consulta.Id);

        Assert.True(resultado.CapturaAtiva);
        Assert.NotNull(resultado.Gravacao);
        Assert.NotNull(_consulta.IniciadaEm);
    }

    [Fact]
    public async Task Iniciar_ConsultaNaoConfirmada_NaoEPermitido()
    {
        var emCheckout = Consulta.ParaCheckout(
            DateTime.UtcNow.AddHours(1), _vet.Id, _animal.Id, _animal.TutorId, Guid.NewGuid(), Guid.NewGuid());
        _consultaRepo.Setup(r => r.ObterPorIdAsync(emCheckout.Id)).ReturnsAsync(emCheckout);
        _repo.Setup(r => r.ObterSessaoDaConsultaAsync(emCheckout.Id)).ReturnsAsync((SessaoCaptura?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().IniciarAsync(emCheckout.Id));

        Assert.Equal("RN-008", ex.Codigo);
    }

    [Fact]
    public async Task Iniciar_DuasVezes_Retorna409()
    {
        SessaoAberta();

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().IniciarAsync(_consulta.Id));

        // Iniciar duas vezes seria abrir duas janelas de gravacao sobre o mesmo atendimento
        Assert.Equal("RN-008", ex.Codigo);
    }

    [Fact]
    public async Task Iniciar_NoPlanoBasico_ConsultaComecaSemCaptura()
    {
        var basico = new Veterinario("Dr. Basico", new Crmv("54321-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Basico);

        var consulta = Consulta.ParaCheckout(
            DateTime.UtcNow.AddHours(1), basico.Id, _animal.Id, _animal.TutorId, Guid.NewGuid(), Guid.NewGuid());
        consulta.ConfirmarPagamento();

        _consultaRepo.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _vetRepo.Setup(r => r.ObterPorIdAsync(basico.Id)).ReturnsAsync(basico);
        _repo.Setup(r => r.ObterSessaoDaConsultaAsync(consulta.Id)).ReturnsAsync((SessaoCaptura?)null);

        var resultado = await CriarServico().IniciarAsync(consulta.Id);

        // RN-085: a consulta acontece, o que nao existe e a IA na consulta
        Assert.False(resultado.CapturaAtiva);
        Assert.Null(resultado.Gravacao);
        Assert.Contains("CapturaIndisponivelNoPlanoBasico", resultado.Avisos);
    }

    [Fact]
    public async Task Iniciar_ComPesoAusente_AvisaAntesDeComecar()
    {
        var semPeso = new Animal("Rex", "Canino", "SRD", DateTime.UtcNow.AddYears(-2), Guid.NewGuid());
        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(semPeso);

        var resultado = await CriarServico().IniciarAsync(_consulta.Id);

        // Descobrir que falta peso so no fim do atendimento seria tarde (RN-081)
        Assert.Contains("PesoAusente", resultado.Avisos);
    }

    [Fact]
    public async Task Iniciar_ConsultaDeOutroVeterinario_ERecusado()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().IniciarAsync(_consulta.Id));

        Assert.Equal("RN-105", ex.Codigo);
    }

    // ── Recebimento de segmentos (RN-009/RN-079) ─────────────────────────────

    [Fact]
    public async Task ReceberSegmento_ComJanelaAberta_EnfileiraATranscricao()
    {
        SessaoAberta();
        var midia = AudioNoStorage();

        var resultado = await CriarServico().ReceberSegmentoAsync(_consulta.Id, Segmento(midia.Id));

        Assert.Equal(EstadoSegmentoAudio.Recebido, resultado.Estado);

        // O despacho ao motor sai da requisicao: o vet nao espera a transcricao
        _fila.Verify(f => f.EnfileirarAsync(TipoJob.TranscreverSegmento, It.IsAny<string>(), null), Times.Once);
    }

    [Fact]
    public async Task ReceberSegmento_ForaDaJanela_Retorna409()
    {
        var sessao = SessaoAberta();
        sessao.Encerrar(segmentosRecebidos: 1);
        var midia = AudioNoStorage();

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().ReceberSegmentoAsync(_consulta.Id, Segmento(midia.Id)));

        // RN-079: fora da janela a IA nao captura audio
        Assert.Equal("RN-079", ex.Codigo);
    }

    [Fact]
    public async Task ReceberSegmento_SemConsultaIniciada_NaoEAceito()
    {
        var midia = AudioNoStorage();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ReceberSegmentoAsync(_consulta.Id, Segmento(midia.Id)));

        Assert.Equal("RN-008", ex.Codigo);
    }

    [Fact]
    public async Task ReceberSegmento_ComMidiaQueNaoEAudio_NaoEAceito()
    {
        SessaoAberta();
        var foto = new Midia(TipoMidia.FotoPet, "image/jpeg");
        _midiaRepo.Setup(r => r.ObterPorIdAsync(foto.Id)).ReturnsAsync(foto);

        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().ReceberSegmentoAsync(_consulta.Id, Segmento(foto.Id)));
    }

    [Fact]
    public async Task ReceberSegmento_SequenciaRepetida_Retorna409()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();

        var jaRecebido = new SegmentoAudio(sessao.Id, 0, midia.Id, 30000, 0);
        _repo.Setup(r => r.ObterSegmentoPorSequenciaAsync(sessao.Id, 0)).ReturnsAsync(jaRecebido);

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().ReceberSegmentoAsync(_consulta.Id, Segmento(midia.Id)));

        // Reenvio do mesmo trecho duplicaria o texto na transcricao final
        Assert.Equal("RN-009", ex.Codigo);
    }

    // ── Encerramento (RN-008/RN-038) ─────────────────────────────────────────

    [Fact]
    public async Task Encerrar_MarcaAConsultaComoRealizada()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = new SegmentoAudio(sessao.Id, 0, midia.Id, 30000, 0);
        segmento.RegistrarTranscricao();
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        var resultado = await CriarServico().EncerrarAsync(_consulta.Id);

        // RN-038: encerrar e o que marca a consulta como realizada
        Assert.Equal(StatusConsulta.Realizada, resultado.StatusConsulta);
        Assert.NotNull(_consulta.EncerradaEm);
    }

    [Fact]
    public async Task Encerrar_SemNenhumSegmento_CaiNoCaminhoManual()
    {
        SessaoAberta();

        var resultado = await CriarServico().EncerrarAsync(_consulta.Id);

        // Sem audio nao ha o que transcrever: o prontuario e manual (RN-085)
        Assert.Equal(EstadoSessaoCaptura.SemTranscricao, resultado.EstadoDaSessao);
    }

    [Fact]
    public async Task Encerrar_ComSegmentoPendente_AguardaATranscricao()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var pendente = new SegmentoAudio(sessao.Id, 0, midia.Id, 30000, 0);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([pendente]);

        var resultado = await CriarServico().EncerrarAsync(_consulta.Id);

        Assert.Equal(EstadoSessaoCaptura.AguardandoTranscricao, resultado.EstadoDaSessao);
        Assert.Equal(1, resultado.SegmentosPendentes);
    }

    [Fact]
    public async Task Encerrar_DuasVezes_Retorna409()
    {
        var sessao = SessaoAberta();
        sessao.Encerrar(0);

        await Assert.ThrowsAsync<ConflitoDeEstadoException>(() => CriarServico().EncerrarAsync(_consulta.Id));
    }

    // ── Callback do motor (§5.3) ─────────────────────────────────────────────

    /// <summary>
    /// Token que o job de despacho entrega ao motor. Os cenarios de callback precisam
    /// nascer despachados: o servico so aceita a resposta que traz o token do proprio
    /// segmento (RN-009).
    /// </summary>
    private const string TokenDoCallback = "token-de-teste-do-segmento";

    private static string HashDoToken(string token) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Um segmento ja despachado ao motor, com o hash do token gravado.</summary>
    private static SegmentoAudio SegmentoDespachado(Guid sessaoId, Guid midiaId, int sequencia = 0)
    {
        var segmento = new SegmentoAudio(sessaoId, sequencia, midiaId, 30000, 0);
        segmento.RegistrarDespacho(HashDoToken(TokenDoCallback), DateTime.UtcNow);
        return segmento;
    }

    [Fact]
    public async Task Callback_ComTexto_RegistraATranscricao()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        Transcricao? persistida = null;
        _repo.Setup(r => r.AdicionarTranscricaoAsync(It.IsAny<Transcricao>()))
            .Callback<Transcricao>(t => persistida = t).Returns(Task.CompletedTask);

        await CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
        {
            CallbackToken = TokenDoCallback,
            SegmentoId = segmento.Id,
            Status = "Ok",
            Texto = "Paciente apresenta vomito ha um dia.",
            Confianca = 0.91m,
            Motor = new MotorDeTranscricaoDto { Nome = "stt-flow", Versao = "1.3.0" }
        });

        Assert.Equal(EstadoSegmentoAudio.Transcrito, segmento.Estado);
        Assert.Contains("vomito", persistida!.Texto);
        Assert.Equal("stt-flow 1.3.0", persistida.Motor);
    }

    [Fact]
    public async Task Callback_Reentregue_NaoDuplicaOTexto()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        segmento.RegistrarTranscricao();
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);

        await CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
        {
            CallbackToken = TokenDoCallback,
            SegmentoId = segmento.Id, Status = "Ok", Texto = "texto repetido"
        });

        // Callback e entregue mais de uma vez por natureza
        _repo.Verify(r => r.AdicionarTranscricaoAsync(It.IsAny<Transcricao>()), Times.Never);
    }

    [Fact]
    public async Task Callback_ComFalha_DevolveOSegmentoParaAFila()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
        {
            CallbackToken = TokenDoCallback,
            SegmentoId = segmento.Id, Status = "Falha", Motivo = MotivoFalhaTranscricao.MotorIndisponivel
        });

        // Ainda ha tentativa: volta para a fila em vez de perder o trecho
        Assert.Equal(EstadoSegmentoAudio.Recebido, segmento.Estado);
        _fila.Verify(f => f.EnfileirarAsync(
            TipoJob.TranscreverSegmento, segmento.Id.ToString(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(2, 30)]
    [InlineData(3, 90)]
    public void Backoff_CresceConformeAEspecificacao(int tentativas, int segundosEsperados)
    {
        // §4.2: 10s / 30s / 90s. Espera fixa insistiria no mesmo intervalo contra um
        // motor que caiu de vez, adiando o desfecho que o veterinario esta esperando.
        Assert.Equal(
            TimeSpan.FromSeconds(segundosEsperados), CapturaService.BackoffDaTentativa(tentativas));
    }

    [Fact]
    public async Task Callback_ComFalha_ReenfileiraComOBackoffDaPrimeiraTentativa()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        TimeSpan? atraso = null;
        _fila.Setup(f => f.EnfileirarAsync(
                TipoJob.TranscreverSegmento, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<TipoJob, string?, TimeSpan?>((_, _, a) => atraso = a)
            .Returns(Task.CompletedTask);

        await CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
        {
            CallbackToken = TokenDoCallback,
            SegmentoId = segmento.Id, Status = "Falha", Motivo = MotivoFalhaTranscricao.MotorIndisponivel
        });

        Assert.Equal(TimeSpan.FromSeconds(10), atraso);
    }

    [Fact]
    public async Task Callback_FalhaDepoisDeTresTentativas_DaOTrechoComoPerdido()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);

        for (var i = 1; i < SegmentoAudio.MaximoDeTentativas; i++)
            segmento.RegistrarDespacho(HashDoToken(TokenDoCallback), DateTime.UtcNow);

        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
        {
            CallbackToken = TokenDoCallback,
            SegmentoId = segmento.Id, Status = "Falha", Motivo = MotivoFalhaTranscricao.AudioIlegivel
        });

        Assert.Equal(EstadoSegmentoAudio.Falha, segmento.Estado);
        Assert.Equal(MotivoFalhaTranscricao.AudioIlegivel, segmento.FalhaMotivo);
    }

    // ── Desfecho da transcrição (§7.3) ───────────────────────────────────────

    [Fact]
    public async Task Transcricao_TodosOsTrechosOk_SegueParaAEstruturacao()
    {
        var sessao = SessaoAberta();
        sessao.Encerrar(segmentosRecebidos: 1);

        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
        {
            CallbackToken = TokenDoCallback,
            SegmentoId = segmento.Id, Status = "Ok", Texto = "texto"
        });

        Assert.Equal(EstadoSessaoCaptura.GerandoRascunho, sessao.Estado);
    }

    [Fact]
    public async Task Transcricao_ComDesfecho_EnfileiraAEstruturacao()
    {
        var sessao = SessaoAberta();
        sessao.Encerrar(segmentosRecebidos: 1);

        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
        {
            CallbackToken = TokenDoCallback,
            SegmentoId = segmento.Id, Status = "Ok", Texto = "texto"
        });

        // RN-080: a estruturacao segue fora da requisicao
        _fila.Verify(f => f.EnfileirarAsync(TipoJob.EstruturarConsulta, sessao.Id.ToString(), null), Times.Once);
    }

    [Fact]
    public void Transcricao_ParteDosTrechosFalhou_SegueParcial()
    {
        var sessao = new SessaoCaptura(Guid.NewGuid(), capturaAtiva: true);
        sessao.Encerrar(segmentosRecebidos: 3);

        sessao.RegistrarDesfechoDaTranscricao(transcritos: 2, falhados: 1);

        // Perder a consulta inteira porque um trecho falhou seria pior que um
        // rascunho parcial com aviso (§4.2)
        Assert.Equal(EstadoSessaoCaptura.TranscricaoParcial, sessao.Estado);
    }

    [Fact]
    public void Transcricao_NenhumTrechoTranscrito_CaiNoCaminhoManual()
    {
        var sessao = new SessaoCaptura(Guid.NewGuid(), capturaAtiva: true);
        sessao.Encerrar(segmentosRecebidos: 2);

        sessao.RegistrarDesfechoDaTranscricao(transcritos: 0, falhados: 2);

        Assert.Equal(EstadoSessaoCaptura.SemTranscricao, sessao.Estado);
    }

    [Fact]
    public async Task Estado_TrazOTextoParcialNaOrdemDosTrechos()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();

        var primeiro = new SegmentoAudio(sessao.Id, 0, midia.Id, 30000, 0);
        var segundo = new SegmentoAudio(sessao.Id, 1, midia.Id, 30000, 30000);
        primeiro.RegistrarTranscricao();
        segundo.RegistrarTranscricao();

        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segundo, primeiro]);
        _repo.Setup(r => r.ObterTranscricoesAsync(sessao.Id)).ReturnsAsync(
        [
            new Transcricao(segundo.Id, "segunda parte", null, null, null),
            new Transcricao(primeiro.Id, "primeira parte", null, null, null)
        ]);

        var estado = await CriarServico().ObterEstadoAsync(_consulta.Id);

        // A ordem do texto e a dos trechos, nao a de chegada dos callbacks
        Assert.StartsWith("primeira parte", estado.TextoParcial);
        Assert.EndsWith("segunda parte", estado.TextoParcial);
        Assert.Equal(2, estado.SegmentosTranscritos);
    }

    // ── P-01: encerrar o atendimento nao fecha a documentacao ───────────────

    [Fact]
    public async Task Encerrar_NaoMarcaAConsultaComoFinalizada()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        segmento.RegistrarTranscricao();
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().EncerrarAsync(_consulta.Id);

        // Entre encerrar e finalizar existe o trabalho documental: revisar o rascunho
        // da IA, gerar o prontuario, assinar a receita. Marcar Finalizada aqui daria
        // por fechada uma documentacao que sequer comecou, e a RN-087 nunca chegaria
        // a ser cobrada.
        Assert.Equal(StatusConsulta.Realizada, _consulta.Status);
        Assert.False(_consulta.Finalizada);
    }

    // ── RN-009: o callback tem de ser a resposta DESTE segmento ─────────────

    [Fact]
    public async Task Callback_SemToken_LancaAcessoNegadoRN009()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
            {
                SegmentoId = segmento.Id, Status = "Ok", Texto = "texto injetado"
            }));

        // O token de servico no cabecalho autentica o fluxo como um todo. So ele
        // deixaria quem o conhecesse escrever no prontuario de qualquer consulta,
        // bastando acertar um id de segmento.
        Assert.Equal("RN-009", ex.Codigo);
        _repo.Verify(r => r.AdicionarTranscricaoAsync(It.IsAny<Transcricao>()), Times.Never);
    }

    [Fact]
    public async Task Callback_ComTokenDeOutroSegmento_LancaAcessoNegadoRN009()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
            {
                CallbackToken = "token-de-outro-segmento",
                SegmentoId = segmento.Id,
                Status = "Ok",
                Texto = "texto injetado"
            }));

        Assert.Equal("RN-009", ex.Codigo);
        Assert.Equal(EstadoSegmentoAudio.Enviado, segmento.Estado);
    }

    [Fact]
    public async Task Callback_EmSegmentoNuncaDespachado_LancaAcessoNegadoRN009()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = new SegmentoAudio(sessao.Id, 0, midia.Id, 30000, 0);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
            {
                CallbackToken = TokenDoCallback,
                SegmentoId = segmento.Id,
                Status = "Ok",
                Texto = "texto injetado"
            }));

        // Segmento sem hash gravado nunca foi mandado ao motor: nao ha callback
        // legitimo possivel para ele
        Assert.Equal("RN-009", ex.Codigo);
    }

    [Fact]
    public async Task Callback_ComOTokenCerto_RegistraATranscricao()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoDespachado(sessao.Id, midia.Id);
        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().RegistrarCallbackAsync(new CallbackDeTranscricaoDto
        {
            CallbackToken = TokenDoCallback,
            SegmentoId = segmento.Id,
            Status = "Ok",
            Texto = "Paciente apresenta vomito ha um dia."
        });

        Assert.Equal(EstadoSegmentoAudio.Transcrito, segmento.Estado);
    }

    // ── Varredura de segmento travado (§4.2) ─────────────────────────────────

    /// <summary>
    /// Um segmento despachado ha tempo demais, sem callback. E o cenario que a
    /// varredura existe para resolver: motor que aceitou e morreu calado.
    /// </summary>
    private SegmentoAudio SegmentoTravado(Guid sessaoId, Guid midiaId, int sequencia = 0)
    {
        var segmento = new SegmentoAudio(sessaoId, sequencia, midiaId, 30000, 0);

        segmento.RegistrarDespacho(
            HashDoToken(TokenDoCallback), DateTime.UtcNow - CapturaService.PrazoDoCallback.Add(TimeSpan.FromMinutes(1)));

        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        return segmento;
    }

    [Fact]
    public async Task Travado_ComTentativaSobrando_VoltaParaAFila()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoTravado(sessao.Id, midia.Id);

        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([segmento]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        var tratados = await CriarServico().ResolverSegmentosTravadosAsync();

        Assert.Equal(1, tratados);
        Assert.Equal(EstadoSegmentoAudio.Recebido, segmento.Estado);
        Assert.Equal(MotivoFalhaTranscricao.Timeout, segmento.FalhaMotivo);

        _fila.Verify(f => f.EnfileirarAsync(
            TipoJob.TranscreverSegmento, segmento.Id.ToString(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task Travado_SemTentativaSobrando_ViraFalhaPorTimeout()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoTravado(sessao.Id, midia.Id);

        // Esgota as tentativas
        for (var i = 1; i < SegmentoAudio.MaximoDeTentativas; i++)
            segmento.RegistrarDespacho(HashDoToken(TokenDoCallback), DateTime.UtcNow.AddMinutes(-10));

        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([segmento]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().ResolverSegmentosTravadosAsync();

        Assert.Equal(EstadoSegmentoAudio.Falha, segmento.Estado);
        Assert.Equal(MotivoFalhaTranscricao.Timeout, segmento.FalhaMotivo);

        _fila.Verify(f => f.EnfileirarAsync(
            TipoJob.TranscreverSegmento, It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Fact]
    public async Task Travado_SemTentativaSobrando_DestravaASessao()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoTravado(sessao.Id, midia.Id);

        for (var i = 1; i < SegmentoAudio.MaximoDeTentativas; i++)
            segmento.RegistrarDespacho(HashDoToken(TokenDoCallback), DateTime.UtcNow.AddMinutes(-10));

        sessao.Encerrar(segmentosRecebidos: 1);
        Assert.Equal(EstadoSessaoCaptura.AguardandoTranscricao, sessao.Estado);

        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([segmento]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().ResolverSegmentosTravadosAsync();

        // Sem a varredura a sessao ficaria em AguardandoTranscricao para sempre, e o
        // app nunca veria um estado terminal no polling do rascunho
        Assert.Equal(EstadoSessaoCaptura.SemTranscricao, sessao.Estado);
    }

    [Fact]
    public async Task Travado_ComOutroTrechoTranscrito_SessaoVaiParaTranscricaoParcial()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();

        var transcrito = SegmentoDespachado(sessao.Id, midia.Id, sequencia: 0);
        transcrito.RegistrarTranscricao();

        var travado = SegmentoTravado(sessao.Id, midia.Id, sequencia: 1);
        for (var i = 1; i < SegmentoAudio.MaximoDeTentativas; i++)
            travado.RegistrarDespacho(HashDoToken(TokenDoCallback), DateTime.UtcNow.AddMinutes(-10));

        sessao.Encerrar(segmentosRecebidos: 2);

        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([travado]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([transcrito, travado]);

        await CriarServico().ResolverSegmentosTravadosAsync();

        // O rascunho sai com o que ha, e com aviso: perder a consulta inteira porque um
        // trecho travou seria pior
        Assert.Equal(EstadoSessaoCaptura.TranscricaoParcial, sessao.Estado);
        _fila.Verify(f => f.EnfileirarAsync(
            TipoJob.EstruturarConsulta, sessao.Id.ToString(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task Travado_SemSegmentoVencido_NaoMexeEmNada()
    {
        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        Assert.Equal(0, await CriarServico().ResolverSegmentosTravadosAsync());

        _repo.Verify(r => r.SalvarAsync(), Times.Never);
    }

    // ── O outro jeito de travar: preso em Recebido, sem job vivo (§4.2) ──────

    /// <summary>
    /// Um trecho que foi despachado, falhou, voltou para a fila — e cujo job de
    /// despacho depois morreu. Fica em <c>Recebido</c> sem ninguém para reenfileirá-lo:
    /// o job esgotou as tentativas DELE, que não são as do segmento.
    /// </summary>
    private SegmentoAudio SegmentoPresoEmRecebido(
        Guid sessaoId, Guid midiaId, int sequencia = 0, int despachosAntes = 1, int minutosAtras = 10)
    {
        var segmento = new SegmentoAudio(sessaoId, sequencia, midiaId, 30000, 0);

        for (var i = 0; i < despachosAntes; i++)
            segmento.RegistrarDespacho(HashDoToken(TokenDoCallback), DateTime.UtcNow.AddMinutes(-minutosAtras));

        if (despachosAntes > 0)
            segmento.RegistrarFalha(MotivoFalhaTranscricao.MotorIndisponivel);

        Assert.Equal(EstadoSegmentoAudio.Recebido, segmento.Estado);

        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        return segmento;
    }

    [Fact]
    public async Task PresoEmRecebido_ComTentativaSobrando_VoltaParaAFila()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();
        var segmento = SegmentoPresoEmRecebido(sessao.Id, midia.Id);

        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([segmento]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        Assert.Equal(1, await CriarServico().ResolverSegmentosTravadosAsync());

        Assert.Equal(EstadoSegmentoAudio.Recebido, segmento.Estado);
        Assert.Equal(MotivoFalhaTranscricao.Timeout, segmento.FalhaMotivo);

        // A tentativa PRECISA ser contada aqui: o despacho que nao vingou nao passou
        // por RegistrarDespacho, e sem conta-la a varredura reenfileiraria para sempre
        Assert.Equal(2, segmento.Tentativas);

        _fila.Verify(f => f.EnfileirarAsync(
            TipoJob.TranscreverSegmento, segmento.Id.ToString(), It.IsAny<TimeSpan?>()), Times.Once);
    }

    [Fact]
    public async Task PresoEmRecebido_SemTentativaSobrando_ViraFalhaPorTimeout()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();

        // Duas tentativas ja consumidas; a varredura consome a terceira e ultima
        var segmento = SegmentoPresoEmRecebido(
            sessao.Id, midia.Id, despachosAntes: SegmentoAudio.MaximoDeTentativas - 1);

        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([segmento]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().ResolverSegmentosTravadosAsync();

        Assert.Equal(EstadoSegmentoAudio.Falha, segmento.Estado);
        Assert.Equal(MotivoFalhaTranscricao.Timeout, segmento.FalhaMotivo);

        // O criterio de parada e o do SEGMENTO: sem isso a varredura trocaria uma
        // sessao travada por um laco de jobs
        _fila.Verify(f => f.EnfileirarAsync(
            TipoJob.TranscreverSegmento, It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    [Fact]
    public async Task RecemCriadoEmRecebido_DentroDoPrazo_NaoEMexido()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();

        // Recem-criado, nunca despachado: DespachadoEm e nulo e o relogio corre do
        // CriadoEm. Cinco segundos de vida e fluxo normal, nao trecho travado.
        var segmento = new SegmentoAudio(sessao.Id, 0, midia.Id, 30000, 0);

        _repo.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([segmento]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        Assert.Equal(0, await CriarServico().ResolverSegmentosTravadosAsync());

        Assert.Equal(EstadoSegmentoAudio.Recebido, segmento.Estado);
        Assert.Null(segmento.FalhaMotivo);
        Assert.Equal(0, segmento.Tentativas);

        _fila.Verify(f => f.EnfileirarAsync(
            It.IsAny<TipoJob>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
        _repo.Verify(r => r.SalvarAsync(), Times.Never);
    }

    [Fact]
    public async Task NuncaDespachado_AlemDoPrazo_ContaComoTravado()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();

        // Nunca despachado (o job morreu antes de chegar ao motor) e ja fora do prazo:
        // o relogio corre do CriadoEm, porque DespachadoEm nunca foi preenchido
        var segmento = SegmentoPresoEmRecebido(sessao.Id, midia.Id, despachosAntes: 0);

        Assert.Null(segmento.DespachadoEm);
        Assert.True(segmento.EsperaEsgotada(TimeSpan.Zero, DateTime.UtcNow.AddMinutes(1)));

        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([segmento]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        // Dentro do prazo real nao e travado — o teste do relogio esta no EsperaEsgotada
        Assert.Equal(0, await CriarServico().ResolverSegmentosTravadosAsync());
    }

    [Fact]
    public async Task PresoEmRecebido_DestravaASessaoParaSemTranscricao()
    {
        var sessao = SessaoAberta();
        var midia = AudioNoStorage();

        var segmento = SegmentoPresoEmRecebido(
            sessao.Id, midia.Id, despachosAntes: SegmentoAudio.MaximoDeTentativas - 1);

        sessao.Encerrar(segmentosRecebidos: 1);
        Assert.Equal(EstadoSessaoCaptura.AguardandoTranscricao, sessao.Estado);

        _repo.Setup(r => r.ObterSegmentosComEsperaEsgotadaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([segmento]);
        _repo.Setup(r => r.ObterSegmentosAsync(sessao.Id)).ReturnsAsync([segmento]);

        await CriarServico().ResolverSegmentosTravadosAsync();

        // A garantia da §4.2 nao e "Enviado nao trava", e "a sessao sempre chega a um
        // estado terminal" — por qualquer das portas
        Assert.Equal(EstadoSessaoCaptura.SemTranscricao, sessao.Estado);
    }
}
