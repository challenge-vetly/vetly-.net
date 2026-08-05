using Moq;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Application.Strategies.Fidelidade;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do FidelidadeService.
/// Cobre a pontuacao de consulta (cumprimento de obrigacao x avulsa — RN-070), o estorno
/// antifraude (RN-075) e o calculo de desconto por tier respeitando a penalidade de
/// no-show (RN-064/072).
/// </summary>
public class FidelidadeServiceTests
{
    private readonly Mock<IPontosFidelidadeRepository> _pontosRepoMock = new();
    private readonly Mock<IObrigacaoDoPetRepository> _obrigacaoRepoMock = new();
    private readonly Mock<IResponsavelRepository> _responsavelRepoMock = new();
    private static readonly IDescontoFidelidadeStrategy[] TodasAsStrategies =
        [new DescontoBronzeStrategy(), new DescontoPrataStrategy(), new DescontoOuroStrategy()];
    private readonly FakeTimeProvider _timeProvider = new(new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc));

    private FidelidadeService CriarServico() =>
        new(_pontosRepoMock.Object, _obrigacaoRepoMock.Object, _responsavelRepoMock.Object, TodasAsStrategies, _timeProvider);

    private static Responsavel CriarResponsavel() =>
        new("Responsavel Teste", "responsavel@teste.com", "11999999999");

    [Fact]
    public async Task PontuarConsultaRealizadaAsync_ComObrigacaoPendenteNoPrazo_MarcaCumpridaEDaPontosCheios()
    {
        var animalId = Guid.NewGuid();
        var responsavel = CriarResponsavel();
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var obrigacao = new ObrigacaoDoPet(animalId, TipoObrigacao.Vacina, agora.AddDays(5));
        var consultaId = Guid.NewGuid();

        _obrigacaoRepoMock.Setup(r => r.ObterPendenteMaisProximaAsync(animalId, TipoObrigacao.Vacina)).ReturnsAsync(obrigacao);
        _pontosRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<PontosFidelidade>())).Returns(Task.CompletedTask);
        _pontosRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _pontosRepoMock.Setup(r => r.ObterPorResponsavelAsync(responsavel.Id)).ReturnsAsync([]);

        await CriarServico().PontuarConsultaRealizadaAsync(consultaId, animalId, responsavel.Id, TipoServico.Vacinacao, agora);

        Assert.Equal(StatusObrigacao.Cumprida, obrigacao.Status);
        _pontosRepoMock.Verify(r => r.AdicionarAsync(It.Is<PontosFidelidade>(
            p => p.Origem == OrigemPontos.ObrigacaoCumprida && p.Pontos == 50)), Times.Once);
        _obrigacaoRepoMock.Verify(r => r.Atualizar(obrigacao), Times.Once);
    }

    [Fact]
    public async Task PontuarConsultaRealizadaAsync_SemObrigacaoPendente_PontuaComoAvulsaComPesoMenor()
    {
        var animalId = Guid.NewGuid();
        var responsavel = CriarResponsavel();
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var consultaId = Guid.NewGuid();

        _obrigacaoRepoMock.Setup(r => r.ObterPendenteMaisProximaAsync(animalId, TipoObrigacao.Vacina)).ReturnsAsync((ObrigacaoDoPet?)null);
        _pontosRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<PontosFidelidade>())).Returns(Task.CompletedTask);
        _pontosRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _pontosRepoMock.Setup(r => r.ObterPorResponsavelAsync(responsavel.Id)).ReturnsAsync([]);

        await CriarServico().PontuarConsultaRealizadaAsync(consultaId, animalId, responsavel.Id, TipoServico.Vacinacao, agora);

        _pontosRepoMock.Verify(r => r.AdicionarAsync(It.Is<PontosFidelidade>(
            p => p.Origem == OrigemPontos.ConsultaAvulsa && p.Pontos == 20)), Times.Once);
    }

    [Fact]
    public async Task PontuarConsultaRealizadaAsync_ObrigacaoPendenteMasVencida_PontuaComoAvulsa()
    {
        var animalId = Guid.NewGuid();
        var responsavel = CriarResponsavel();
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var obrigacaoVencida = new ObrigacaoDoPet(animalId, TipoObrigacao.Vacina, agora.AddDays(-1));

        _obrigacaoRepoMock.Setup(r => r.ObterPendenteMaisProximaAsync(animalId, TipoObrigacao.Vacina)).ReturnsAsync(obrigacaoVencida);
        _pontosRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<PontosFidelidade>())).Returns(Task.CompletedTask);
        _pontosRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _pontosRepoMock.Setup(r => r.ObterPorResponsavelAsync(responsavel.Id)).ReturnsAsync([]);

        await CriarServico().PontuarConsultaRealizadaAsync(Guid.NewGuid(), animalId, responsavel.Id, TipoServico.Vacinacao, agora);

        Assert.Equal(StatusObrigacao.Pendente, obrigacaoVencida.Status); // nao marcada — fora do prazo
        _pontosRepoMock.Verify(r => r.AdicionarAsync(It.Is<PontosFidelidade>(p => p.Origem == OrigemPontos.ConsultaAvulsa)), Times.Once);
    }

    [Fact]
    public async Task PontuarConsultaRealizadaAsync_TipoServicoSemMapeamento_PontuaComoAvulsaSemConsultarObrigacoes()
    {
        var animalId = Guid.NewGuid();
        var responsavel = CriarResponsavel();
        var agora = _timeProvider.GetUtcNow().UtcDateTime;

        _pontosRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<PontosFidelidade>())).Returns(Task.CompletedTask);
        _pontosRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _pontosRepoMock.Setup(r => r.ObterPorResponsavelAsync(responsavel.Id)).ReturnsAsync([]);

        await CriarServico().PontuarConsultaRealizadaAsync(Guid.NewGuid(), animalId, responsavel.Id, TipoServico.Cirurgia, agora);

        _obrigacaoRepoMock.Verify(r => r.ObterPendenteMaisProximaAsync(It.IsAny<Guid>(), It.IsAny<TipoObrigacao>()), Times.Never);
        _pontosRepoMock.Verify(r => r.AdicionarAsync(It.Is<PontosFidelidade>(p => p.Origem == OrigemPontos.ConsultaAvulsa)), Times.Once);
    }

    [Fact]
    public async Task EstornarPontosPorCancelamentoAsync_SemLancamentoParaAConsulta_NaoFazNada()
    {
        var consultaId = Guid.NewGuid();
        _pontosRepoMock.Setup(r => r.ObterPorConsultaAsync(consultaId)).ReturnsAsync((PontosFidelidade?)null);

        await CriarServico().EstornarPontosPorCancelamentoAsync(consultaId, _timeProvider.GetUtcNow().UtcDateTime);

        _pontosRepoMock.Verify(r => r.Atualizar(It.IsAny<PontosFidelidade>()), Times.Never);
    }

    [Fact]
    public async Task EstornarPontosPorCancelamentoAsync_ComLancamentoValido_EstornaERecalculaTier()
    {
        var responsavel = CriarResponsavel();
        var consultaId = Guid.NewGuid();
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var lancamento = new PontosFidelidade(responsavel.Id, consultaId, OrigemPontos.ObrigacaoCumprida, 50, agora);

        _pontosRepoMock.Setup(r => r.ObterPorConsultaAsync(consultaId)).ReturnsAsync(lancamento);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _pontosRepoMock.Setup(r => r.ObterPorResponsavelAsync(responsavel.Id)).ReturnsAsync([]);

        await CriarServico().EstornarPontosPorCancelamentoAsync(consultaId, agora);

        Assert.True(lancamento.Estornado);
        _responsavelRepoMock.Verify(r => r.Atualizar(responsavel), Times.Once);
    }

    [Fact]
    public async Task CalcularDescontoAsync_ResponsavelSobPenalidadeDeNoShow_RetornaDescontoZeradoComFlag()
    {
        var responsavel = CriarResponsavel();
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        responsavel.RegistrarNoShow(agora);
        responsavel.RegistrarNoShow(agora);
        responsavel.RegistrarNoShow(agora); // 3o no-show bloqueia descontos por 60 dias
        responsavel.RecalcularFidelidade(800); // mesmo Ouro, a penalidade zera o desconto

        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);

        var resultado = await CriarServico().CalcularDescontoAsync(responsavel.Id, 200m, agora);

        Assert.True(resultado.BloqueadoPorPenalidade);
        Assert.Equal(0m, resultado.ValorDesconto);
    }

    [Fact]
    public async Task CalcularDescontoAsync_TierPrata_CalculaDescontoEIncidenciaCorretos()
    {
        var responsavel = CriarResponsavel();
        responsavel.RecalcularFidelidade(300); // Prata
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);

        var resultado = await CriarServico().CalcularDescontoAsync(responsavel.Id, 200m, _timeProvider.GetUtcNow().UtcDateTime);

        Assert.Equal(TierFidelidade.Prata, resultado.TierFidelidade);
        Assert.Equal(10m, resultado.ValorDesconto); // 5% de 200
        Assert.Equal(6m, resultado.IncidenciaVetly); // 3% de 200
        Assert.Equal(4m, resultado.IncidenciaVeterinario); // 2% de 200
        Assert.False(resultado.BloqueadoPorPenalidade);
    }

    [Fact]
    public async Task ObterFidelidadeAsync_TierBronze_CalculaPontosParaProximoTier()
    {
        var responsavel = CriarResponsavel();
        responsavel.RecalcularFidelidade(100);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);

        var resultado = await CriarServico().ObterFidelidadeAsync(responsavel.Id);

        Assert.Equal(200, resultado.PontosParaProximoTier); // faltam 200 para os 300 do Prata
    }

    [Fact]
    public async Task ObterFidelidadeAsync_TierOuro_PontosParaProximoTierENulo()
    {
        var responsavel = CriarResponsavel();
        responsavel.RecalcularFidelidade(900);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);

        var resultado = await CriarServico().ObterFidelidadeAsync(responsavel.Id);

        Assert.Null(resultado.PontosParaProximoTier);
    }
}
