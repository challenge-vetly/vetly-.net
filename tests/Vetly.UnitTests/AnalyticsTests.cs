using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Metricas agregadas da plataforma (RN-106).
///
/// Sao tres perguntas: o agendamento esta virando atendimento? o dinheiro esta
/// entrando? a IA esta ajudando ou dando trabalho?
/// </summary>
public class AnalyticsTests
{
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IAuditoriaIaRepository> _auditoria = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    public AnalyticsTests()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        _consultaRepo.Setup(r => r.ObterNoPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _pagamentoRepo.Setup(r => r.ObterConfirmadosNoPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _auditoria.Setup(r => r.ObterNoPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
    }

    private AnalyticsService CriarServico() =>
        new(_consultaRepo.Object, _pagamentoRepo.Object, _auditoria.Object, _usuario.Object);

    private static Consulta Consulta(StatusConsulta status, bool pago = true)
    {
        var consulta = Domain.Entities.Consulta.ParaCheckout(
            DateTime.UtcNow.AddDays(-1), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());

        if (pago)
            consulta.ConfirmarPagamento();

        switch (status)
        {
            case StatusConsulta.Realizada: consulta.Finalizar(); break;
            case StatusConsulta.Cancelada: consulta.Cancelar(); break;
            case StatusConsulta.NoShow: consulta.RegistrarNoShow(); break;
            case StatusConsulta.Expirada: consulta.Expirar(); break;
        }

        return consulta;
    }

    private void Consultas(params Consulta[] consultas) =>
        _consultaRepo.Setup(r => r.ObterNoPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(consultas);

    private static Pagamento Pagamento(decimal valor = 200m, decimal comissao = 24m)
    {
        var pagamento = new Pagamento(Guid.NewGuid(), valor, MeioPagamento.Pix, Guid.NewGuid());
        pagamento.Confirmar();
        pagamento.RegistrarSplit(PlanoAssinatura.Profissional, 12m, comissao, valor - comissao, Guid.NewGuid());

        return pagamento;
    }

    private void Pagamentos(params Pagamento[] pagamentos) =>
        _pagamentoRepo.Setup(r => r.ObterConfirmadosNoPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(pagamentos);

    private static LogAuditoriaIa Decisao(DecisaoSobreRascunho decisao) =>
        new(Guid.NewGuid(), null, decisao == DecisaoSobreRascunho.Manual ? null : Guid.NewGuid(),
            Guid.NewGuid(), decisao, "{}", null, decisao != DecisaoSobreRascunho.Aprovado, null);

    private void Decisoes(params LogAuditoriaIa[] decisoes) =>
        _auditoria.Setup(r => r.ObterNoPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(decisoes);

    // ── Funil (RN-035/RN-038) ────────────────────────────────────────────────

    [Fact]
    public async Task Funil_ContaCadaDesfechoDaConsulta()
    {
        Consultas(
            Consulta(StatusConsulta.Realizada),
            Consulta(StatusConsulta.Realizada),
            Consulta(StatusConsulta.Cancelada),
            Consulta(StatusConsulta.NoShow),
            Consulta(StatusConsulta.Expirada, pago: false));

        var metricas = await CriarServico().ObterDaPlataformaAsync(null, null);

        Assert.Equal(5, metricas.Funil.Criadas);
        Assert.Equal(2, metricas.Funil.Realizadas);
        Assert.Equal(1, metricas.Funil.Canceladas);
        Assert.Equal(1, metricas.Funil.NoShow);
        Assert.Equal(1, metricas.Funil.Expiradas);
    }

    [Fact]
    public async Task Funil_CalculaAsTaxasSobreOsDenominadoresCertos()
    {
        Consultas(
            Consulta(StatusConsulta.Realizada),
            Consulta(StatusConsulta.Realizada),
            Consulta(StatusConsulta.Cancelada),
            Consulta(StatusConsulta.Expirada, pago: false));

        var funil = (await CriarServico().ObterDaPlataformaAsync(null, null)).Funil;

        // Conversao e sobre tudo que foi criado: 2 de 4
        Assert.Equal(50m, funil.TaxaDeConversao);

        // Cancelamento e sobre o que chegou a ser pago: 1 de 3. A consulta expirada
        // nunca foi paga, e conta-la faria a taxa parecer melhor do que e.
        Assert.Equal(33.33m, funil.TaxaDeCancelamento);
    }

    [Fact]
    public async Task Funil_SemMovimento_NaoEstoura()
    {
        var funil = (await CriarServico().ObterDaPlataformaAsync(null, null)).Funil;

        // Periodo sem movimento e situacao normal, nao erro
        Assert.Equal(0m, funil.TaxaDeConversao);
        Assert.Equal(0m, funil.TaxaDeCancelamento);
    }

    // ── Uso da IA (RN-082) ───────────────────────────────────────────────────

    [Fact]
    public async Task Ia_MedeQuantosRascunhosForamAceitosSemCorrecao()
    {
        Decisoes(
            Decisao(DecisaoSobreRascunho.Aprovado),
            Decisao(DecisaoSobreRascunho.Aprovado),
            Decisao(DecisaoSobreRascunho.Corrigido),
            Decisao(DecisaoSobreRascunho.NaoAprovado));

        var ia = (await CriarServico().ObterDaPlataformaAsync(null, null)).Ia;

        // Correcao alta significa que a IA esta dando trabalho em vez de poupar
        Assert.Equal(50m, ia.TaxaDeAprovacaoSemCorrecao);
        Assert.Equal(25m, ia.TaxaDeRecusa);
    }

    [Fact]
    public async Task Ia_ProntuarioManualFicaForaDoDenominador()
    {
        Decisoes(
            Decisao(DecisaoSobreRascunho.Aprovado),
            Decisao(DecisaoSobreRascunho.Manual),
            Decisao(DecisaoSobreRascunho.Manual));

        var ia = (await CriarServico().ObterDaPlataformaAsync(null, null)).Ia;

        // Prontuario manual nao e rascunho recusado: e atendimento que nunca teve
        // rascunho, e conta-lo faria a IA parecer pior do que e
        Assert.Equal(2, ia.ProntuariosManuais);
        Assert.Equal(100m, ia.TaxaDeAprovacaoSemCorrecao);
        Assert.Equal(3, ia.DecisoesRegistradas);
    }

    [Fact]
    public async Task Ia_SemDecisao_NaoEstoura()
    {
        var ia = (await CriarServico().ObterDaPlataformaAsync(null, null)).Ia;

        Assert.Equal(0m, ia.TaxaDeAprovacaoSemCorrecao);
        Assert.Equal(0, ia.DecisoesRegistradas);
    }

    // ── Receita (RN-070) ─────────────────────────────────────────────────────

    [Fact]
    public async Task Receita_SomaOBrutoEOTicketMedio()
    {
        Pagamentos(Pagamento(), Pagamento(valor: 400m, comissao: 48m));

        var receita = (await CriarServico().ObterDaPlataformaAsync(null, null)).Receita;

        Assert.Equal(2, receita.TransacoesConfirmadas);
        Assert.Equal(600m, receita.ValorBruto);
        Assert.Equal(300m, receita.TicketMedio);
    }

    [Fact]
    public async Task Receita_TakeRateEfetivoSaiDoQueFoiRetidoDeVerdade()
    {
        Pagamentos(Pagamento());

        var receita = (await CriarServico().ObterDaPlataformaAsync(null, null)).Receita;

        // Efetivo, e nao nominal: o desconto de fidelidade sai da comissao, entao o
        // que a plataforma retem de fato pode ser menor que o take rate do plano
        Assert.Equal(12m, receita.TakeRateEfetivo);
    }

    [Fact]
    public async Task Receita_SemTransacao_NaoEstoura()
    {
        var receita = (await CriarServico().ObterDaPlataformaAsync(null, null)).Receita;

        Assert.Equal(0m, receita.TicketMedio);
        Assert.Equal(0m, receita.TakeRateEfetivo);
    }

    // ── Período e escopo (RN-106) ────────────────────────────────────────────

    [Fact]
    public async Task Periodo_SemParametro_UsaOsUltimosTrintaDias()
    {
        var metricas = await CriarServico().ObterDaPlataformaAsync(null, null);

        var dias = (metricas.PeriodoFim - metricas.PeriodoInicio).TotalDays;

        // A janela em que uma metrica ainda reage ao que foi mudado: trimestre inteiro
        // esconde a semana ruim
        Assert.Equal(30, Math.Round(dias));
    }

    [Fact]
    public async Task Periodo_Invertido_NaoEAceito()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CriarServico()
            .ObterDaPlataformaAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(-10)));
    }

    [Fact]
    public async Task Metricas_PorQuemNaoEAdmin_SaoRecusadas()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterDaPlataformaAsync(null, null));

        Assert.Equal("RN-106", ex.Codigo);
    }
}
