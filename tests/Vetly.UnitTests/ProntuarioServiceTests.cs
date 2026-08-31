using System.Text.Json;
using Moq;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Decisao do veterinario sobre o rascunho da IA e prontuario manual
/// (RN-082/RN-085, §7.3).
///
/// A IA sugere; quem decide e o veterinario, e a decisao fica registrada.
/// </summary>
public class ProntuarioServiceTests
{
    private readonly Mock<ICapturaRepository> _captura = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IAuditoriaIaRepository> _auditoria = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _vetId = Guid.NewGuid();
    private readonly Consulta _consulta;
    private readonly SessaoCaptura _sessao;
    private readonly RascunhoIa _rascunho;

    private LogAuditoriaIa? _registrado;

    public ProntuarioServiceTests()
    {
        _consulta = Consulta.ParaCheckout(
            DateTime.UtcNow, _vetId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        _consulta.ConfirmarPagamento();
        _consulta.Finalizar();

        _sessao = new SessaoCaptura(_consulta.Id, capturaAtiva: true);
        _sessao.Encerrar(segmentosRecebidos: 1);
        _sessao.RegistrarDesfechoDaTranscricao(transcritos: 1, falhados: 0);
        _sessao.RascunhoDisponivel();

        _rascunho = new RascunhoIa(
            _sessao.Id, _consulta.Id,
            "Vomito ha dois dias.", "Abdome sensivel.", ["Gastrite aguda"],
            "Jejum de 12h.", "Retornar se persistir.",
            "texto falado", "ollama/llama3.1", parcial: false, [], 900);

        _usuario.SetupGet(u => u.EhAdmin).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vetId);

        _consultaRepo.Setup(r => r.ObterPorIdAsync(_consulta.Id)).ReturnsAsync(_consulta);
        _consultaRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _captura.Setup(r => r.ObterRascunhoDaConsultaAsync(_consulta.Id)).ReturnsAsync(_rascunho);
        _captura.Setup(r => r.ObterSessaoDaConsultaAsync(_consulta.Id)).ReturnsAsync(_sessao);
        _captura.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _auditoria.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _auditoria.Setup(r => r.AdicionarAsync(It.IsAny<LogAuditoriaIa>()))
            .Callback<LogAuditoriaIa>(l => _registrado = l).Returns(Task.CompletedTask);
    }

    private ProntuarioService CriarServico() =>
        new(_captura.Object, _consultaRepo.Object, _auditoria.Object, _usuario.Object);

    private static ConteudoDoProntuarioDto Conteudo(string anamnese = "Escrito pelo veterinario.") => new()
    {
        Anamnese = anamnese,
        ExameFisico = "Sem alteracoes.",
        HipotesesDiagnosticas = ["Gastrite"],
        Conduta = "Antiemetico.",
        Orientacoes = "Dieta leve."
    };

    // ── Aprovar (RN-082) ─────────────────────────────────────────────────────

    [Fact]
    public async Task Aprovar_ValidaODiagnosticoESegueParaOsDocumentos()
    {
        var resultado = await CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
        {
            Decisao = DecisaoSobreRascunho.Aprovado
        });

        Assert.True(resultado.DiagnosticoValidado);
        Assert.True(_consulta.DiagnosticoValidado);
        Assert.Equal(EstadoSessaoCaptura.Documentando, _sessao.Estado);
        Assert.Equal(DecisaoSobreRascunho.Aprovado, _rascunho.Decisao);
    }

    [Fact]
    public async Task Aprovar_GravaOConteudoDaIANaTrilhaSemMarcarAlteracao()
    {
        await CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
        {
            Decisao = DecisaoSobreRascunho.Aprovado
        });

        Assert.NotNull(_registrado);
        Assert.False(_registrado.AlterouSugestao);
        Assert.Equal("ollama/llama3.1", _registrado.Modelo);
        Assert.Equal(_vetId, _registrado.VeterinarioId);

        // O conteudo final inteiro, e nao um diff: reconstruir o que foi aceito a
        // partir de diferencas e fragil justamente quando mais importa
        var conteudo = JsonSerializer.Deserialize<ConteudoDoProntuarioDto>(
            _registrado.ConteudoFinal, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Contains("Vomito", conteudo.Anamnese);
        Assert.Equal("Gastrite aguda", conteudo.HipotesesDiagnosticas[0]);
    }

    // ── Corrigir (RN-082) ────────────────────────────────────────────────────

    [Fact]
    public async Task Corrigir_GravaOConteudoDoVeterinarioEMarcaAAlteracao()
    {
        var resultado = await CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
        {
            Decisao = DecisaoSobreRascunho.Corrigido,
            Correcao = Conteudo("Vomito ha tres dias, nao dois."),
            Justificativa = "A IA errou a duracao do quadro."
        });

        Assert.True(resultado.DiagnosticoValidado);

        // Aprovar sem ler e corrigir antes de aprovar nao podem ficar registrados da
        // mesma forma
        Assert.True(_registrado!.AlterouSugestao);
        Assert.Contains("tres dias", _registrado.ConteudoFinal);
        Assert.Equal(DecisaoSobreRascunho.Corrigido, _registrado.Decisao);
    }

    [Fact]
    public async Task Corrigir_SemOConteudoCorrigido_NaoEAceito()
    {
        // Corrigir sem dizer o que mudou nao e corrigir
        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
            {
                Decisao = DecisaoSobreRascunho.Corrigido
            }));
    }

    [Fact]
    public async Task Corrigir_ComConteudoVazio_NaoEAceito()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
            {
                Decisao = DecisaoSobreRascunho.Corrigido,
                Correcao = new ConteudoDoProntuarioDto()
            }));
    }

    // ── Não aprovar (RN-082) ─────────────────────────────────────────────────

    [Fact]
    public async Task NaoAprovar_EncerraSemDocumentosENaoValidaODiagnostico()
    {
        var resultado = await CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
        {
            Decisao = DecisaoSobreRascunho.NaoAprovado,
            Justificativa = "O rascunho descreve outro atendimento."
        });

        // Sem validacao nao se gera documento (RN-082)
        Assert.False(resultado.DiagnosticoValidado);
        Assert.False(_consulta.DiagnosticoValidado);
        Assert.Equal(EstadoSessaoCaptura.EncerradaSemDocumentos, _sessao.Estado);

        // Recusa e desfecho legitimo, e fica registrada como tal
        Assert.Equal(DecisaoSobreRascunho.NaoAprovado, _registrado!.Decisao);
        Assert.Equal(string.Empty, _registrado.ConteudoFinal);
        Assert.Contains("outro atendimento", _registrado.Justificativa);
    }

    [Fact]
    public async Task NaoAprovar_SemJustificativa_NaoEAceito()
    {
        // Recusar sem registrar por que deixa a trilha sem o que mais importa
        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
            {
                Decisao = DecisaoSobreRascunho.NaoAprovado
            }));
    }

    // ── Uma decisão por rascunho ─────────────────────────────────────────────

    [Fact]
    public async Task Decidir_DuasVezes_Retorna409()
    {
        var servico = CriarServico();

        await servico.DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
        {
            Decisao = DecisaoSobreRascunho.Aprovado
        });

        // Uma segunda decisao sobre o mesmo rascunho deixaria a trilha ambigua
        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => servico.DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
            {
                Decisao = DecisaoSobreRascunho.NaoAprovado,
                Justificativa = "mudei de ideia"
            }));

        Assert.Equal("RN-082", ex.Codigo);
    }

    [Fact]
    public async Task Decidir_SemRascunho_Retorna404()
    {
        _captura.Setup(r => r.ObterRascunhoDaConsultaAsync(_consulta.Id)).ReturnsAsync((RascunhoIa?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
            {
                Decisao = DecisaoSobreRascunho.Aprovado
            }));
    }

    [Fact]
    public async Task Decidir_ConsultaCancelada_NaoEPermitido()
    {
        _consulta.Cancelar();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
            {
                Decisao = DecisaoSobreRascunho.Aprovado
            }));

        Assert.Equal("CONSULTA-003", ex.Codigo);
    }

    [Fact]
    public async Task Decidir_ConsultaDeOutroVeterinario_ERecusado()
    {
        _usuario.SetupGet(u => u.VeterinarioId).Returns(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
            {
                Decisao = DecisaoSobreRascunho.Aprovado
            }));

        Assert.Equal("RN-105", ex.Codigo);
    }

    // ── Prontuário manual (RN-085) ───────────────────────────────────────────

    [Fact]
    public async Task ProntuarioManual_SemIANoCaminho_ValidaODiagnosticoEFicaNaTrilha()
    {
        _captura.Setup(r => r.ObterRascunhoDaConsultaAsync(_consulta.Id)).ReturnsAsync((RascunhoIa?)null);

        var resultado = await CriarServico().RegistrarManualAsync(
            _consulta.Id, new ProntuarioManualDto { Conteudo = Conteudo() });

        // Conteudo escrito pelo proprio veterinario ja e conteudo validado
        Assert.True(resultado.DiagnosticoValidado);
        Assert.Equal(DecisaoSobreRascunho.Manual, _registrado!.Decisao);
        Assert.Null(_registrado.RascunhoIaId);
        Assert.Null(_registrado.Modelo);
        Assert.False(_registrado.AlterouSugestao);
    }

    [Fact]
    public async Task ProntuarioManual_DepoisDeRecusarORascunho_EAceito()
    {
        var servico = CriarServico();

        await servico.DecidirAsync(_consulta.Id, new DecisaoDoProntuarioDto
        {
            Decisao = DecisaoSobreRascunho.NaoAprovado,
            Justificativa = "O rascunho nao corresponde ao atendimento."
        });

        var resultado = await servico.RegistrarManualAsync(
            _consulta.Id, new ProntuarioManualDto { Conteudo = Conteudo() });

        // O atendimento aconteceu e precisa virar prontuario de algum jeito (RN-085)
        Assert.True(resultado.DiagnosticoValidado);
        Assert.Equal(EstadoSessaoCaptura.Documentando, _sessao.Estado);
    }

    [Fact]
    public async Task ProntuarioManual_ComRascunhoPendente_Retorna409()
    {
        // Ficariam dois prontuarios concorrentes sobre o mesmo atendimento
        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().RegistrarManualAsync(
                _consulta.Id, new ProntuarioManualDto { Conteudo = Conteudo() }));

        Assert.Equal("RN-082", ex.Codigo);
    }

    [Fact]
    public async Task ProntuarioManual_SemConteudoClinico_NaoEAceito()
    {
        _captura.Setup(r => r.ObterRascunhoDaConsultaAsync(_consulta.Id)).ReturnsAsync((RascunhoIa?)null);

        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().RegistrarManualAsync(
                _consulta.Id, new ProntuarioManualDto { Conteudo = new ConteudoDoProntuarioDto() }));
    }

    [Fact]
    public async Task ProntuarioManual_SemCaptura_FuncionaSemSessao()
    {
        // Consulta de emergencia pelo balcao nunca abriu janela de captura (RN-040)
        _captura.Setup(r => r.ObterRascunhoDaConsultaAsync(_consulta.Id)).ReturnsAsync((RascunhoIa?)null);
        _captura.Setup(r => r.ObterSessaoDaConsultaAsync(_consulta.Id)).ReturnsAsync((SessaoCaptura?)null);

        var resultado = await CriarServico().RegistrarManualAsync(
            _consulta.Id, new ProntuarioManualDto { Conteudo = Conteudo() });

        Assert.True(resultado.DiagnosticoValidado);
        Assert.Null(resultado.EstadoDaSessao);
    }

    // ── Trilha de auditoria (RN-082) ─────────────────────────────────────────

    [Fact]
    public async Task Auditoria_AcumulaAsDecisoesDaConsulta()
    {
        _auditoria.Setup(r => r.ObterDaConsultaAsync(_consulta.Id)).ReturnsAsync(
        [
            new LogAuditoriaIa(_consulta.Id, _sessao.Id, null, _vetId,
                DecisaoSobreRascunho.Manual, "{}", null, false, null),
            new LogAuditoriaIa(_consulta.Id, _sessao.Id, _rascunho.Id, _vetId,
                DecisaoSobreRascunho.NaoAprovado, string.Empty, "nao corresponde", true, "ollama/llama3.1")
        ]);

        var trilha = await CriarServico().ObterAuditoriaAsync(_consulta.Id);

        // A mesma consulta acumula decisoes: a recusa e o prontuario manual que a sucede
        Assert.Equal(2, trilha.Count);
        Assert.Contains(trilha, l => l.Decisao == DecisaoSobreRascunho.NaoAprovado);
        Assert.Contains(trilha, l => l.Decisao == DecisaoSobreRascunho.Manual);
    }

    [Fact]
    public async Task Auditoria_DeOutroVeterinario_ERecusada()
    {
        _usuario.SetupGet(u => u.VeterinarioId).Returns(Guid.NewGuid());

        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterAuditoriaAsync(_consulta.Id));
    }
}
