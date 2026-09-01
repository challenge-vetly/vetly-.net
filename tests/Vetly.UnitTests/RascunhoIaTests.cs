using Moq;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Estruturacao da consulta pela IA (RN-080, §7.3).
///
/// O que a IA produz e rascunho ate o veterinario decidir (RN-082).
/// </summary>
public class RascunhoIaTests
{
    private readonly Mock<ICapturaRepository> _repo = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IOllamaService> _ia = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Veterinario _vet;
    private readonly Animal _animal;
    private readonly Consulta _consulta;
    private readonly SessaoCaptura _sessao;
    private readonly SegmentoAudio _segmento;

    public RascunhoIaTests()
    {
        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());
        _animal.RegistrarPeso(31.5m);

        _consulta = Consulta.ParaCheckout(
            DateTime.UtcNow, _vet.Id, _animal.Id, _animal.TutorId, Guid.NewGuid(), Guid.NewGuid());
        _consulta.ConfirmarPagamento();

        _sessao = new SessaoCaptura(_consulta.Id, capturaAtiva: true);
        _sessao.Encerrar(segmentosRecebidos: 1);

        var midiaId = Guid.NewGuid();
        _segmento = new SegmentoAudio(_sessao.Id, 0, midiaId, 30000, 0);
        _segmento.RegistrarTranscricao();

        _usuario.SetupGet(u => u.EhAdmin).Returns(true);
        _repo.Setup(r => r.ObterSessaoAsync(_sessao.Id)).ReturnsAsync(_sessao);
        _repo.Setup(r => r.ObterSessaoDaConsultaAsync(_consulta.Id)).ReturnsAsync(_sessao);
        _repo.Setup(r => r.ObterSegmentosAsync(_sessao.Id)).ReturnsAsync([_segmento]);
        _repo.Setup(r => r.ObterTranscricoesAsync(_sessao.Id)).ReturnsAsync(
            [new Transcricao(_segmento.Id, "O cao esta vomitando ha dois dias.", 0.9m, null, "stt 1.0")]);
        _repo.Setup(r => r.ObterRascunhoDaSessaoAsync(_sessao.Id)).ReturnsAsync((RascunhoIa?)null);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _consultaRepo.Setup(r => r.ObterPorIdAsync(_consulta.Id)).ReturnsAsync(_consulta);
        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(_animal);
    }

    private readonly Mock<IColmeiaService> _colmeia = new();

    private RascunhoService CriarServico() =>
        new(_repo.Object, _consultaRepo.Object, _animalRepo.Object, _ia.Object, _usuario.Object,
            _colmeia.Object);

    private void IaResponde(ConsultaEstruturadaDto resposta) =>
        _ia.Setup(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>())).ReturnsAsync(resposta);

    private static ConsultaEstruturadaDto RespostaCompleta() => new()
    {
        Anamnese = "Vomito ha dois dias, sem diarreia.",
        ExameFisico = "Mucosas normocoradas, abdome sensivel.",
        HipotesesDiagnosticas = ["Gastrite aguda", "Corpo estranho"],
        Conduta = "Jejum de 12h e antiemetico.",
        Orientacoes = "Retornar se o vomito persistir."
    };

    // ── Geração do rascunho (RN-080) ─────────────────────────────────────────

    [Fact]
    public async Task Gerar_ComTranscricao_ProduzORascunhoEDeixaASessaoPronta()
    {
        _sessao.RegistrarDesfechoDaTranscricao(transcritos: 1, falhados: 0);
        IaResponde(RespostaCompleta());

        RascunhoIa? persistido = null;
        _repo.Setup(r => r.AdicionarRascunhoAsync(It.IsAny<RascunhoIa>()))
            .Callback<RascunhoIa>(r => persistido = r).Returns(Task.CompletedTask);

        await CriarServico().GerarAsync(_sessao.Id);

        Assert.NotNull(persistido);
        Assert.Equal("Gastrite aguda", persistido.HipotesesDiagnosticas[0]);
        Assert.Equal(EstadoSessaoCaptura.RascunhoPronto, _sessao.Estado);
    }

    [Fact]
    public async Task Gerar_GuardaOTextoDeOrigemJuntoDoRascunho()
    {
        _sessao.RegistrarDesfechoDaTranscricao(1, 0);
        IaResponde(RespostaCompleta());

        RascunhoIa? persistido = null;
        _repo.Setup(r => r.AdicionarRascunhoAsync(It.IsAny<RascunhoIa>()))
            .Callback<RascunhoIa>(r => persistido = r).Returns(Task.CompletedTask);

        await CriarServico().GerarAsync(_sessao.Id);

        // Sem o texto de origem nao ha como conferir depois se a IA produziu algo que
        // nao foi dito na consulta
        Assert.Contains("vomitando", persistido!.TextoOrigem);
        Assert.NotNull(persistido.Modelo);
    }

    [Fact]
    public async Task Gerar_EntregaAIAOContextoClinicoDoAnimal()
    {
        _sessao.RegistrarDesfechoDaTranscricao(1, 0);
        IaResponde(RespostaCompleta());

        ContextoDaEstruturacaoDto? contexto = null;
        _ia.Setup(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()))
            .Callback<ContextoDaEstruturacaoDto>(c => contexto = c)
            .ReturnsAsync(RespostaCompleta());

        await CriarServico().GerarAsync(_sessao.Id);

        // Especie e peso mudam a leitura clinica do que foi dito
        Assert.Equal("Canino", contexto!.Especie);
        Assert.Equal(31.5m, contexto.PesoKg);
        Assert.False(contexto.TranscricaoParcial);
    }

    [Fact]
    public async Task Gerar_ComTranscricaoParcial_AvisaNoContextoENoRascunho()
    {
        _sessao.RegistrarDesfechoDaTranscricao(transcritos: 1, falhados: 1);

        ContextoDaEstruturacaoDto? contexto = null;
        _ia.Setup(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()))
            .Callback<ContextoDaEstruturacaoDto>(c => contexto = c)
            .ReturnsAsync(RespostaCompleta());

        RascunhoIa? persistido = null;
        _repo.Setup(r => r.AdicionarRascunhoAsync(It.IsAny<RascunhoIa>()))
            .Callback<RascunhoIa>(r => persistido = r).Returns(Task.CompletedTask);

        await CriarServico().GerarAsync(_sessao.Id);

        // A IA precisa saber que o relato esta incompleto para nao preencher a lacuna
        // por conta propria — e o veterinario precisa saber antes de aprovar
        Assert.True(contexto!.TranscricaoParcial);
        Assert.True(persistido!.Parcial);
        Assert.Contains("TranscricaoParcial", persistido.Avisos);
    }

    [Fact]
    public async Task Gerar_ComPesoAusente_AvisaNoRascunho()
    {
        _sessao.RegistrarDesfechoDaTranscricao(1, 0);
        IaResponde(RespostaCompleta());

        var semPeso = new Animal("Rex", "Canino", "SRD", DateTime.UtcNow.AddYears(-2), Guid.NewGuid());
        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(semPeso);

        RascunhoIa? persistido = null;
        _repo.Setup(r => r.AdicionarRascunhoAsync(It.IsAny<RascunhoIa>()))
            .Callback<RascunhoIa>(r => persistido = r).Returns(Task.CompletedTask);

        await CriarServico().GerarAsync(_sessao.Id);

        // Sem peso nao ha sugestao de dose (RN-081)
        Assert.Contains("PesoAusente", persistido!.Avisos);
    }

    [Fact]
    public async Task Gerar_SegundaVez_NaoProduzOutroRascunhoSobreOMesmoAtendimento()
    {
        _sessao.RegistrarDesfechoDaTranscricao(1, 0);

        _repo.Setup(r => r.ObterRascunhoDaSessaoAsync(_sessao.Id)).ReturnsAsync(new RascunhoIa(
            _sessao.Id, _consulta.Id, "a", "b", ["c"], "d", "e", "origem", "modelo", false, [], 10));

        await CriarServico().GerarAsync(_sessao.Id);

        // Job reentregue e situacao normal, nao motivo para um segundo rascunho
        _ia.Verify(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()), Times.Never);
    }

    [Fact]
    public async Task Gerar_SessaoForaDoEstadoDeEstruturacao_NaoChamaAIA()
    {
        // Ainda capturando: nada a estruturar
        var capturando = new SessaoCaptura(Guid.NewGuid(), capturaAtiva: true);
        _repo.Setup(r => r.ObterSessaoAsync(capturando.Id)).ReturnsAsync(capturando);
        _repo.Setup(r => r.ObterRascunhoDaSessaoAsync(capturando.Id)).ReturnsAsync((RascunhoIa?)null);

        await CriarServico().GerarAsync(capturando.Id);

        _ia.Verify(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()), Times.Never);
    }

    [Fact]
    public async Task Gerar_SemTextoTranscrito_CaiNoCaminhoManual()
    {
        _sessao.RegistrarDesfechoDaTranscricao(1, 0);
        _repo.Setup(r => r.ObterTranscricoesAsync(_sessao.Id)).ReturnsAsync([]);

        await CriarServico().GerarAsync(_sessao.Id);

        // Rascunho vazio nao deve ser oferecido como se fosse prontuario (RN-085)
        Assert.Equal(EstadoSessaoCaptura.SemTranscricao, _sessao.Estado);
        _ia.Verify(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()), Times.Never);
    }

    [Fact]
    public async Task Gerar_IAForaDoAr_CaiNoCaminhoManualEmVezDeTravarAConsulta()
    {
        _sessao.RegistrarDesfechoDaTranscricao(1, 0);
        _ia.Setup(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()))
            .ThrowsAsync(new HttpRequestException("ollama indisponivel"));

        // Propaga para que o worker retente; a sessao nao fica presa no meio do ciclo
        await Assert.ThrowsAsync<HttpRequestException>(() => CriarServico().GerarAsync(_sessao.Id));

        // O atendimento aconteceu e precisa virar prontuario de algum jeito (RN-085)
        Assert.Equal(EstadoSessaoCaptura.SemTranscricao, _sessao.Estado);
    }

    [Fact]
    public async Task Gerar_IADevolveNadaAproveitavel_CaiNoCaminhoManual()
    {
        _sessao.RegistrarDesfechoDaTranscricao(1, 0);
        IaResponde(new ConsultaEstruturadaDto());

        await CriarServico().GerarAsync(_sessao.Id);

        Assert.Equal(EstadoSessaoCaptura.SemTranscricao, _sessao.Estado);
        _repo.Verify(r => r.AdicionarRascunhoAsync(It.IsAny<RascunhoIa>()), Times.Never);
    }

    // ── Leitura do rascunho (RN-082/RN-105) ──────────────────────────────────

    [Fact]
    public async Task Obter_TrazORascunhoComATranscricaoDeOrigem()
    {
        _repo.Setup(r => r.ObterRascunhoDaConsultaAsync(_consulta.Id)).ReturnsAsync(new RascunhoIa(
            _sessao.Id, _consulta.Id, "anamnese", "exame", ["hipotese"], "conduta", "orientacoes",
            "texto falado", "ollama/llama3.1", parcial: true, ["TranscricaoParcial"], 1200));

        var dto = await CriarServico().ObterDaConsultaAsync(_consulta.Id);

        Assert.Equal("anamnese", dto.Anamnese);
        Assert.Equal("texto falado", dto.TextoOrigem);
        Assert.True(dto.Parcial);
        Assert.Contains("TranscricaoParcial", dto.Avisos);
    }

    [Fact]
    public async Task Obter_SemRascunhoAinda_Retorna404()
    {
        _repo.Setup(r => r.ObterRascunhoDaConsultaAsync(_consulta.Id)).ReturnsAsync((RascunhoIa?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().ObterDaConsultaAsync(_consulta.Id));
    }

    [Fact]
    public async Task Obter_ConsultaDeOutroVeterinario_ERecusado()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(Guid.NewGuid());

        // RN-105: rascunho e conteudo clinico do atendimento de quem o conduziu
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterDaConsultaAsync(_consulta.Id));

        Assert.Equal("RN-105", ex.Codigo);
    }

    // ── Invariantes do rascunho ──────────────────────────────────────────────

    [Fact]
    public void Rascunho_SemConteudoClinico_EReconhecidoComoVazio()
    {
        var vazio = new RascunhoIa(
            Guid.NewGuid(), Guid.NewGuid(), "  ", "", [], "", "so orientacoes genericas",
            "origem", "modelo", false, [], 10);

        // Orientacao generica sozinha nao e prontuario
        Assert.True(vazio.EstaVazio());
    }

    [Fact]
    public void Rascunho_ComHipotese_NaoEVazio()
    {
        var comHipotese = new RascunhoIa(
            Guid.NewGuid(), Guid.NewGuid(), "", "", ["Gastrite aguda"], "", "",
            "origem", "modelo", false, [], 10);

        Assert.False(comHipotese.EstaVazio());
    }

    // ── RN-005/RN-064/RN-068: o que a IA recebe alem da transcricao ─────────

    [Fact]
    public async Task Contexto_LevaOsPreSintomasDoResponsavel()
    {
        _consulta.RegistrarPreSintomas(
            """{"queixaPrincipal":"Vomito ha 2 dias","duracaoEmDias":2}""");

        ContextoDaEstruturacaoDto? contexto = null;
        _ia.Setup(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()))
            .Callback<ContextoDaEstruturacaoDto>(c => contexto = c)
            .ReturnsAsync(RespostaCompleta());

        _sessao.RegistrarDesfechoDaTranscricao(transcritos: 1, falhados: 0);

        await CriarServico().GerarAsync(_sessao.Id);

        // E o unico relato de quem convive com o animal, e traz o que a consulta nao
        // repete em voz alta (RN-005/RN-036)
        Assert.NotNull(contexto);
        Assert.Contains("Vomito ha 2 dias", contexto!.PreSintomas);
    }

    [Fact]
    public async Task Contexto_LevaOsAlertasDeSegurancaMesmoSemColmeia()
    {
        _animal.AdicionarAlerta("Alergia a dipirona");

        _colmeia.Setup(c => c.PodeAcessarAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<EscopoAcessoColmeia>()))
            .ReturnsAsync(false);

        ContextoDaEstruturacaoDto? contexto = null;
        _ia.Setup(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()))
            .Callback<ContextoDaEstruturacaoDto>(c => contexto = c)
            .ReturnsAsync(RespostaCompleta());

        _sessao.RegistrarDesfechoDaTranscricao(transcritos: 1, falhados: 0);

        await CriarServico().GerarAsync(_sessao.Id);

        // RN-068: alerta de seguranca nao e ocultavel e nao depende de consentimento —
        // e o dado cuja ausencia pode aparecer numa sugestao de dose
        Assert.Contains("Alergia a dipirona", contexto!.AlertasAtivos);
    }

    [Fact]
    public async Task Contexto_SemColmeia_SoLevaOHistoricoDoProprioVeterinario()
    {
        var outraConsultaMinha = Guid.NewGuid();
        var consultaDeOutro = Guid.NewGuid();

        _consultaRepo.Setup(r => r.ObterPorAnimalAsync(_animal.Id)).ReturnsAsync(
        [
            _consulta,
            new Consulta(DateTime.UtcNow.AddMonths(-2), ModalidadeAtendimento.Presencial,
                _vet.Id, _animal.Id, _animal.TutorId),
            new Consulta(DateTime.UtcNow.AddMonths(-1), ModalidadeAtendimento.Presencial,
                Guid.NewGuid(), _animal.Id, _animal.TutorId)
        ]);

        _animalRepo.Setup(r => r.ObterHistoricoLongitudinalAsync(_animal.Id)).ReturnsAsync(
        [
            new Prontuario(outraConsultaMinha, _animal.Id, "Gastrite tratada com sucesso."),
            new Prontuario(consultaDeOutro, _animal.Id, "Fratura tratada em outra clinica.")
        ]);

        _colmeia.Setup(c => c.PodeAcessarAsync(
                _vet.Id, _animal.Id, EscopoAcessoColmeia.HistoricoCompleto))
            .ReturnsAsync(false);

        ContextoDaEstruturacaoDto? contexto = null;
        _ia.Setup(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()))
            .Callback<ContextoDaEstruturacaoDto>(c => contexto = c)
            .ReturnsAsync(RespostaCompleta());

        _sessao.RegistrarDesfechoDaTranscricao(transcritos: 1, falhados: 0);

        await CriarServico().GerarAsync(_sessao.Id);

        // Uma IA que lesse o historico inteiro quando o profissional nao pode le-lo
        // seria uma forma indireta de contornar o consentimento: o texto voltaria ao
        // vet dentro do rascunho, sem nunca ter passado pela guarda (RN-064/RN-066)
        Assert.Empty(contexto!.HistoricoRelevante);
    }

    [Fact]
    public async Task Contexto_ComColmeia_LevaOHistoricoDaRede()
    {
        var consultaDeOutro = Guid.NewGuid();

        _consultaRepo.Setup(r => r.ObterPorAnimalAsync(_animal.Id)).ReturnsAsync([_consulta]);

        _animalRepo.Setup(r => r.ObterHistoricoLongitudinalAsync(_animal.Id)).ReturnsAsync(
        [
            new Prontuario(consultaDeOutro, _animal.Id, "Fratura tratada em outra clinica.")
        ]);

        _colmeia.Setup(c => c.PodeAcessarAsync(
                _vet.Id, _animal.Id, EscopoAcessoColmeia.HistoricoCompleto))
            .ReturnsAsync(true);

        ContextoDaEstruturacaoDto? contexto = null;
        _ia.Setup(i => i.EstruturarConsultaAsync(It.IsAny<ContextoDaEstruturacaoDto>()))
            .Callback<ContextoDaEstruturacaoDto>(c => contexto = c)
            .ReturnsAsync(RespostaCompleta());

        _sessao.RegistrarDesfechoDaTranscricao(transcritos: 1, falhados: 0);

        await CriarServico().GerarAsync(_sessao.Id);

        Assert.Single(contexto!.HistoricoRelevante);
        Assert.Contains("Fratura", contexto.HistoricoRelevante[0]);
    }

    [Fact]
    public async Task Contexto_RegistraOAcessoDaIaAoHistorico()
    {
        _consultaRepo.Setup(r => r.ObterPorAnimalAsync(_animal.Id)).ReturnsAsync([_consulta]);
        _animalRepo.Setup(r => r.ObterHistoricoLongitudinalAsync(_animal.Id)).ReturnsAsync([]);

        IaResponde(RespostaCompleta());

        _sessao.RegistrarDesfechoDaTranscricao(transcritos: 1, falhados: 0);

        await CriarServico().GerarAsync(_sessao.Id);

        // RN-067: quem le em nome do veterinario continua sendo o veterinario, e o
        // Responsavel tem direito de ver isso no log
        _colmeia.Verify(c => c.RegistrarAcessoAsync(
            _animal.Id, EscopoAcessoColmeia.HistoricoCompleto, It.IsAny<bool>(), It.IsAny<string>()),
            Times.Once);
    }
}
