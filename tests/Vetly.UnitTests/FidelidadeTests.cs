using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Programa de fidelidade: pontos por consulta realizada e desconto no resgate
/// (RN-051/RN-052).
/// </summary>
public class FidelidadeTests
{
    private readonly Mock<IFidelidadeRepository> _repo = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();

    public FidelidadeTests()
    {
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<MovimentoDePontos>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repo.Setup(r => r.ObterDoTutorAsync(It.IsAny<Guid>())).ReturnsAsync([]);
        _repo.Setup(r => r.ObterCreditoDaConsultaAsync(It.IsAny<Guid>())).ReturnsAsync((MovimentoDePontos?)null);
        _repo.Setup(r => r.ObterCreditosVencidosSemBaixaAsync(It.IsAny<DateTime>())).ReturnsAsync([]);
    }

    private FidelidadeService CriarServico() =>
        new(_repo.Object, _consultaRepo.Object, _pagamentoRepo.Object, _usuario.Object);

    private void ComSaldo(params MovimentoDePontos[] movimentos) =>
        _repo.Setup(r => r.ObterDoTutorAsync(_tutorId)).ReturnsAsync(movimentos);

    /// <summary>Consulta realizada e paga, o cenário que gera crédito.</summary>
    private Consulta AtendimentoPago(decimal valor = 200m, bool confirmarPagamento = true, bool realizada = true)
    {
        var consulta = Consulta.ParaCheckout(
            DateTime.UtcNow.AddDays(-1), Guid.NewGuid(), Guid.NewGuid(), _tutorId,
            Guid.NewGuid(), Guid.NewGuid());

        consulta.ConfirmarPagamento();

        if (realizada)
            consulta.Finalizar();

        var pagamento = new Pagamento(_tutorId, valor, MeioPagamento.Pix, consulta.Id);

        if (confirmarPagamento)
            pagamento.Confirmar();

        _consultaRepo.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _pagamentoRepo.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(pagamento);

        return consulta;
    }

    // ── Crédito por consulta (RN-052) ────────────────────────────────────────

    [Fact]
    public async Task Credito_PorConsultaRealizadaEPaga_DaUmPontoPorReal()
    {
        var consulta = AtendimentoPago(valor: 180m);

        var movimento = await CriarServico().CreditarPorConsultaAsync(consulta.Id);

        Assert.NotNull(movimento);
        Assert.Equal(180, movimento.Pontos);
        Assert.Equal(TipoMovimentoDePontos.Credito, movimento.Tipo);
    }

    [Fact]
    public async Task Credito_NasceComValidade()
    {
        var consulta = AtendimentoPago();

        var movimento = await CriarServico().CreditarPorConsultaAsync(consulta.Id);

        // Ponto que nunca expira vira passivo eterno
        Assert.NotNull(movimento!.ExpiraEm);
        Assert.True(movimento.ExpiraEm > DateTime.UtcNow.AddDays(360));
    }

    [Fact]
    public async Task Credito_ConsultaNaoRealizada_NaoGeraPonto()
    {
        var consulta = AtendimentoPago(realizada: false);

        Assert.Null(await CriarServico().CreditarPorConsultaAsync(consulta.Id));
    }

    [Fact]
    public async Task Credito_PagamentoNaoConfirmado_NaoGeraPonto()
    {
        var consulta = AtendimentoPago(confirmarPagamento: false);

        // O programa pagaria por receita que nao entrou
        Assert.Null(await CriarServico().CreditarPorConsultaAsync(consulta.Id));
    }

    [Fact]
    public async Task Credito_JaLancado_NaoCreditaDeNovo()
    {
        var consulta = AtendimentoPago();

        _repo.Setup(r => r.ObterCreditoDaConsultaAsync(consulta.Id))
            .ReturnsAsync(MovimentoDePontos.PorConsulta(_tutorId, consulta.Id, 200m));

        // Job reentregue e situacao normal, nao motivo para creditar duas vezes
        Assert.Null(await CriarServico().CreditarPorConsultaAsync(consulta.Id));
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<MovimentoDePontos>()), Times.Never);
    }

    // ── Saldo (RN-052) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Saldo_EASomaDosLancamentos()
    {
        ComSaldo(
            MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 200m),
            MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 150m),
            MovimentoDePontos.PorResgate(_tutorId, 100, 1m, Guid.NewGuid()));

        var saldo = await CriarServico().ObterSaldoAsync(_tutorId);

        // 200 + 150 - 100
        Assert.Equal(250, saldo.Saldo);
        Assert.Equal(2.50m, saldo.ValorEmReais);
        Assert.True(saldo.PodeResgatar);
    }

    [Fact]
    public async Task Saldo_AbaixoDoMinimo_NaoPodeResgatar()
    {
        ComSaldo(MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 50m));

        var saldo = await CriarServico().ObterSaldoAsync(_tutorId);

        Assert.False(saldo.PodeResgatar);
        Assert.Equal(MovimentoDePontos.MinimoParaResgate, saldo.MinimoParaResgate);
    }

    [Fact]
    public async Task Saldo_DeOutroResponsavel_ERecusado()
    {
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterSaldoAsync(Guid.NewGuid()));

        Assert.Equal("RN-106", ex.Codigo);
    }

    // ── Resgate (RN-051) ─────────────────────────────────────────────────────

    [Fact]
    public async Task Resgate_AbateOValorDaCobranca()
    {
        ComSaldo(MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 1000m));

        var desconto = await CriarServico().ApurarDescontoAsync(_tutorId, 500, valorDaCobranca: 200m, teto: 24m);

        Assert.Equal(5.00m, desconto.ValorDoDesconto);
        Assert.Equal(195m, desconto.ValorFinal);
    }

    [Fact]
    public async Task Resgate_AbaixoDoMinimo_NaoEAceito()
    {
        ComSaldo(MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 1000m));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ApurarDescontoAsync(_tutorId, 50, 200m, 24m));

        Assert.Equal("RN-051", ex.Codigo);
    }

    [Fact]
    public async Task Resgate_SemSaldo_NaoEAceito()
    {
        ComSaldo(MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 150m));

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ApurarDescontoAsync(_tutorId, 500, 200m, 24m));

        Assert.Contains("Saldo insuficiente", ex.Message);
    }

    [Fact]
    public async Task Resgate_AcimaDoTeto_DizQuantoCabe()
    {
        ComSaldo(MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 10000m));

        // Teto de R$ 24,00 = 2400 pontos. A Vetly banca a propria fidelidade, mas nao
        // paga para atender.
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ApurarDescontoAsync(_tutorId, 5000, 200m, 24m));

        Assert.Contains("2400", ex.Message);
    }

    [Fact]
    public async Task Resgate_RegistraODebitoComOValorEmReais()
    {
        MovimentoDePontos? debito = null;
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<MovimentoDePontos>()))
            .Callback<MovimentoDePontos>(m => debito = m).Returns(Task.CompletedTask);

        var pagamentoId = Guid.NewGuid();
        await CriarServico().RegistrarResgateAsync(_tutorId, 500, 5m, pagamentoId);

        // O lancamento guarda quanto virou dinheiro: e o que a conferencia financeira
        // precisa cruzar
        Assert.Equal(-500, debito!.Pontos);
        Assert.Equal(5m, debito.ValorEmReais);
        Assert.Equal(pagamentoId, debito.PagamentoId);
    }

    // ── Expiração ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Expiracao_BaixaOCreditoVencidoComLancamentoNoExtrato()
    {
        var vencido = MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 300m);
        typeof(MovimentoDePontos).GetProperty(nameof(MovimentoDePontos.ExpiraEm))!
            .SetValue(vencido, DateTime.UtcNow.AddDays(-1));

        _repo.Setup(r => r.ObterCreditosVencidosSemBaixaAsync(It.IsAny<DateTime>())).ReturnsAsync([vencido]);
        ComSaldo(vencido);

        MovimentoDePontos? baixa = null;
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<MovimentoDePontos>()))
            .Callback<MovimentoDePontos>(m => baixa = m).Returns(Task.CompletedTask);

        var expirados = await CriarServico().ExpirarVencidosAsync();

        Assert.Equal(300, expirados);
        Assert.Equal(TipoMovimentoDePontos.Expiracao, baixa!.Tipo);
        Assert.Equal(-300, baixa.Pontos);

        // A baixa aponta para o credito: e assim que a rotina sabe o que ja processou,
        // sem alterar o lancamento original
        Assert.Equal(vencido.Id, baixa.MovimentoOrigemId);
    }

    [Fact]
    public async Task Expiracao_ComPontosJaGastos_NaoDeixaOSaldoNegativo()
    {
        var vencido = MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 300m);
        typeof(MovimentoDePontos).GetProperty(nameof(MovimentoDePontos.ExpiraEm))!
            .SetValue(vencido, DateTime.UtcNow.AddDays(-1));

        // Ja resgatou 250 dos 300
        var resgate = MovimentoDePontos.PorResgate(_tutorId, 250, 2.50m, Guid.NewGuid());

        _repo.Setup(r => r.ObterCreditosVencidosSemBaixaAsync(It.IsAny<DateTime>())).ReturnsAsync([vencido]);
        ComSaldo(vencido, resgate);

        var expirados = await CriarServico().ExpirarVencidosAsync();

        // Quem resgatou e depois viu o credito vencer nao pode ficar devendo pontos que
        // ja usou legitimamente
        Assert.Equal(50, expirados);
    }

    [Fact]
    public async Task Expiracao_SemCreditoVencido_NaoLancaNada()
    {
        Assert.Equal(0, await CriarServico().ExpirarVencidosAsync());
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<MovimentoDePontos>()), Times.Never);
    }

    // ── Conversão ────────────────────────────────────────────────────────────

    [Fact]
    public void Conversao_CemPontosValemUmReal()
    {
        Assert.Equal(1.00m, MovimentoDePontos.EmReais(100));
        Assert.Equal(12.34m, MovimentoDePontos.EmReais(1234));
    }

    [Fact]
    public void Credito_ComValorZerado_NaoEAceito()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MovimentoDePontos.PorConsulta(_tutorId, Guid.NewGuid(), 0m));
    }
}
