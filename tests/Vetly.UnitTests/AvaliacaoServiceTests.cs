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
/// Testes unitarios do AvaliacaoService.
/// Cobre a unicidade por consulta (RN-076), o recalculo de reputacao do veterinario a
/// cada mutacao do conjunto de avaliacoes validas (RN-078) e a invalidacao antifraude (RN-081).
/// </summary>
public class AvaliacaoServiceTests
{
    private readonly Mock<IAvaliacaoRepository> _repoMock = new();
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc));

    private AvaliacaoService CriarServico() =>
        new(_repoMock.Object, _consultaRepoMock.Object, _vetRepoMock.Object, _timeProvider);

    private static Consulta CriarConsultaRealizada(DateTime dataRealizada)
    {
        var consulta = new Consulta(
            dataRealizada.AddDays(-1), ModalidadeAtendimento.Remoto, TipoServico.Consulta,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.IniciarCheckout(dataRealizada.AddDays(-1));
        consulta.ConfirmarPagamento(dataRealizada.AddDays(-1));
        consulta.MarcarRealizada(dataRealizada);
        return consulta;
    }

    private static Veterinario CriarVeterinario() =>
        new("Dr. Vet", new Crmv("12345-SP"), "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

    [Fact]
    public async Task CriarAsync_ConsultaJaAvaliada_LancaBusinessRuleExceptionAVALIACAO006()
    {
        var consulta = CriarConsultaRealizada(_timeProvider.GetUtcNow().UtcDateTime.AddDays(-1));
        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id))
            .ReturnsAsync(Avaliacao.Criar(
                consulta.Id, Guid.NewGuid(), consulta.VeterinarioId, consulta.Status, consulta.DataRealizada,
                5, null, null, null, null, null, _timeProvider.GetUtcNow().UtcDateTime));

        var dto = new CriarAvaliacaoDto { ResponsavelId = Guid.NewGuid(), NotaGeral = 4 };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CriarServico().CriarAsync(consulta.Id, dto));
        Assert.Equal("AVALIACAO-006", ex.Codigo);
    }

    [Fact]
    public async Task CriarAsync_ConsultaValida_PersisteERecalculaReputacaoDoVeterinario()
    {
        var consulta = CriarConsultaRealizada(_timeProvider.GetUtcNow().UtcDateTime.AddDays(-1));
        var vet = CriarVeterinario();

        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync((Avaliacao?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Avaliacao>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(consulta.VeterinarioId)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.ObterValidasPorVeterinarioAsync(consulta.VeterinarioId))
            .ReturnsAsync([]); // a própria avaliação recém-criada não está no repo real (mock), só valida a chamada

        var dto = new CriarAvaliacaoDto { ResponsavelId = Guid.NewGuid(), NotaGeral = 5 };
        var resultado = await CriarServico().CriarAsync(consulta.Id, dto);

        Assert.Equal(5, resultado.NotaGeral);
        _repoMock.Verify(r => r.AdicionarAsync(It.IsAny<Avaliacao>()), Times.Once);
        _vetRepoMock.Verify(r => r.Atualizar(vet), Times.Once);
    }

    [Fact]
    public async Task EditarAsync_DentroDaJanela_AtualizaERecalculaReputacao()
    {
        var consulta = CriarConsultaRealizada(_timeProvider.GetUtcNow().UtcDateTime.AddDays(-2));
        var vet = CriarVeterinario();
        var avaliacao = Avaliacao.Criar(
            consulta.Id, Guid.NewGuid(), consulta.VeterinarioId, consulta.Status, consulta.DataRealizada,
            3, null, null, null, null, null, _timeProvider.GetUtcNow().UtcDateTime.AddHours(-1));

        _repoMock.Setup(r => r.ObterPorIdAsync(avaliacao.Id)).ReturnsAsync(avaliacao);
        _repoMock.Setup(r => r.ObterValidasPorVeterinarioAsync(avaliacao.VeterinarioId)).ReturnsAsync([avaliacao]);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(avaliacao.VeterinarioId)).ReturnsAsync(vet);

        var dto = new EditarAvaliacaoDto { NotaGeral = 5 };
        var resultado = await CriarServico().EditarAsync(avaliacao.Id, dto);

        Assert.Equal(5, resultado.NotaGeral);
        _vetRepoMock.Verify(r => r.Atualizar(vet), Times.Once);
    }

    [Fact]
    public async Task ResponderAsync_PrimeiraResposta_PersisteResposta()
    {
        var consulta = CriarConsultaRealizada(_timeProvider.GetUtcNow().UtcDateTime.AddDays(-1));
        var avaliacao = Avaliacao.Criar(
            consulta.Id, Guid.NewGuid(), consulta.VeterinarioId, consulta.Status, consulta.DataRealizada,
            4, null, null, null, null, null, _timeProvider.GetUtcNow().UtcDateTime);

        _repoMock.Setup(r => r.ObterPorIdAsync(avaliacao.Id)).ReturnsAsync(avaliacao);

        var resultado = await CriarServico().ResponderAsync(avaliacao.Id, new ResponderAvaliacaoDto { Resposta = "Obrigado!" });

        Assert.Equal("Obrigado!", resultado.RespostaVeterinario);
        _repoMock.Verify(r => r.SalvarAsync(), Times.Once);
    }

    [Fact]
    public async Task ModerarAsync_OcultaComentario_PersisteStatusModeracao()
    {
        var consulta = CriarConsultaRealizada(_timeProvider.GetUtcNow().UtcDateTime.AddDays(-1));
        var avaliacao = Avaliacao.Criar(
            consulta.Id, Guid.NewGuid(), consulta.VeterinarioId, consulta.Status, consulta.DataRealizada,
            4, null, null, null, null, "comentario com dado pessoal", _timeProvider.GetUtcNow().UtcDateTime);

        _repoMock.Setup(r => r.ObterPorIdAsync(avaliacao.Id)).ReturnsAsync(avaliacao);

        var resultado = await CriarServico().ModerarAsync(
            avaliacao.Id, new ModerarAvaliacaoDto { StatusModeracao = StatusModeracao.OcultaPorModeracao });

        Assert.Equal(StatusModeracao.OcultaPorModeracao, resultado.StatusModeracao);
        Assert.Equal(4, resultado.NotaGeral); // moderação nunca altera a nota
    }

    [Fact]
    public async Task InvalidarPorCancelamentoAsync_SemAvaliacaoParaAConsulta_NaoFazNada()
    {
        var consultaId = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorConsultaAsync(consultaId)).ReturnsAsync((Avaliacao?)null);

        await CriarServico().InvalidarPorCancelamentoAsync(consultaId, _timeProvider.GetUtcNow().UtcDateTime);

        _repoMock.Verify(r => r.Atualizar(It.IsAny<Avaliacao>()), Times.Never);
    }

    [Fact]
    public async Task InvalidarPorCancelamentoAsync_ComAvaliacaoExistente_InvalidaERecalculaReputacao()
    {
        var consulta = CriarConsultaRealizada(_timeProvider.GetUtcNow().UtcDateTime.AddDays(-1));
        var vet = CriarVeterinario();
        var avaliacao = Avaliacao.Criar(
            consulta.Id, Guid.NewGuid(), consulta.VeterinarioId, consulta.Status, consulta.DataRealizada,
            5, null, null, null, null, null, _timeProvider.GetUtcNow().UtcDateTime);

        _repoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(avaliacao);
        _repoMock.Setup(r => r.ObterValidasPorVeterinarioAsync(avaliacao.VeterinarioId)).ReturnsAsync([]);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(avaliacao.VeterinarioId)).ReturnsAsync(vet);

        await CriarServico().InvalidarPorCancelamentoAsync(consulta.Id, _timeProvider.GetUtcNow().UtcDateTime);

        Assert.True(avaliacao.Invalidada);
        _vetRepoMock.Verify(r => r.Atualizar(vet), Times.Once);
    }
}
