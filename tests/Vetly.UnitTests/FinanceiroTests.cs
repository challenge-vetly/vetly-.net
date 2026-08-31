using Moq;
using Vetly.Application.DTOs.Financeiro;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Consolidado financeiro e liquidacao de repasses (RN-070/RN-071/RN-072).
///
/// A conta que precisa fechar e uma so: bruto = comissao + repasse + desconto.
/// </summary>
public class FinanceiroTests
{
    private readonly Mock<IPagamentoRepository> _repo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IEmpresaRepository> _empresaRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Veterinario _vet;

    public FinanceiroTests()
    {
        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        _vetRepo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);
        _vetRepo.Setup(r => r.ObterPorIdAsync(It.Is<Guid>(id => id != _vet.Id)))
            .ReturnsAsync((Veterinario?)null);
        _empresaRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Empresa?)null);

        _repo.Setup(r => r.ObterConfirmadosNoPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
    }

    private FinanceiroService CriarServico() =>
        new(_repo.Object, _vetRepo.Object, _empresaRepo.Object, _usuario.Object);

    /// <summary>Uma cobrança confirmada, com o split já apurado.</summary>
    private Pagamento Pagamento(
        decimal valor = 200m,
        decimal comissao = 24m,
        decimal desconto = 0m,
        bool liquidado = false,
        Guid? destinatario = null)
    {
        var pagamento = new Pagamento(Guid.NewGuid(), valor, MeioPagamento.Pix, Guid.NewGuid());
        pagamento.Confirmar();

        if (desconto > 0m)
        {
            var (dv, dp, faixa) = RegrasDeFidelidade.Dividir(desconto);

            pagamento.AplicarDesconto(
                Guid.NewGuid(), RegrasDeFidelidade.PontosPara(desconto), desconto, dv, dp, faixa);
        }

        var repasse = valor - comissao - desconto;
        pagamento.RegistrarSplit(PlanoAssinatura.Profissional, 12m, comissao, repasse, destinatario ?? _vet.Id);

        if (liquidado)
            pagamento.Liquidar();

        return pagamento;
    }

    private void NoPeriodo(params Pagamento[] pagamentos) =>
        _repo.Setup(r => r.ObterConfirmadosNoPeriodoAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(pagamentos);

    private static LiquidarRepasseDto PedidoDeLiquidacao(Guid? destinatario = null) => new()
    {
        DestinatarioId = destinatario,
        Inicio = DateTime.UtcNow.AddDays(-30),
        Fim = DateTime.UtcNow,
        Referencia = "TED-2026-08-001"
    };

    // ── Consolidado (RN-070/RN-072) ──────────────────────────────────────────

    [Fact]
    public async Task Consolidado_SomaBrutoComissaoERepasse()
    {
        NoPeriodo(Pagamento(), Pagamento());

        var consolidado = await CriarServico().ObterConsolidadoAsync(null, null);

        Assert.Equal(2, consolidado.TotalDeTransacoes);
        Assert.Equal(400m, consolidado.ValorBruto);
        Assert.Equal(48m, consolidado.ComissaoLiquida);
        Assert.Equal(352m, consolidado.RepasseTotal);
    }

    [Fact]
    public async Task Consolidado_ComSplitCoerente_Fecha()
    {
        NoPeriodo(Pagamento(), Pagamento(valor: 350m, comissao: 42m));

        var consolidado = await CriarServico().ObterConsolidadoAsync(null, null);

        // Split incoerente e silencioso: os totais continuam somando, e so a conta
        // cruzada revela o problema
        Assert.True(consolidado.Fecha);
    }

    [Fact]
    public async Task Consolidado_ComDescontoDeFidelidade_ContinuaFechando()
    {
        NoPeriodo(Pagamento(desconto: 10m));

        var consolidado = await CriarServico().ObterConsolidadoAsync(null, null);

        // O desconto sai da comissao, e nao do bruto (RN-051)
        Assert.Equal(200m, consolidado.ValorBruto);
        Assert.Equal(10m, consolidado.DescontosDeFidelidade);
        Assert.Equal(166m, consolidado.RepasseTotal);
        Assert.True(consolidado.Fecha);
    }

    [Fact]
    public async Task Consolidado_SeparaOLiquidadoDoPendente()
    {
        NoPeriodo(Pagamento(liquidado: true), Pagamento());

        var consolidado = await CriarServico().ObterConsolidadoAsync(null, null);

        Assert.Equal(176m, consolidado.RepasseLiquidado);
        Assert.Equal(176m, consolidado.RepassePendente);
    }

    [Fact]
    public async Task Consolidado_AgrupaPorDestinatarioComAMaiorPendenciaNaFrente()
    {
        var outroVet = Guid.NewGuid();

        NoPeriodo(
            Pagamento(),
            Pagamento(valor: 1000m, comissao: 120m, destinatario: outroVet));

        var consolidado = await CriarServico().ObterConsolidadoAsync(null, null);

        // E a ordem em que a operacao resolve a fila de pagamento
        Assert.Equal(2, consolidado.PorDestinatario.Count);
        Assert.Equal(outroVet, consolidado.PorDestinatario[0].DestinatarioId);
        Assert.Equal(880m, consolidado.PorDestinatario[0].RepassePendente);
    }

    [Fact]
    public async Task Consolidado_ResolveONomeDoPrestador()
    {
        NoPeriodo(Pagamento());

        var consolidado = await CriarServico().ObterConsolidadoAsync(null, null);

        Assert.Equal("Dra. Marina", consolidado.PorDestinatario[0].Nome);
    }

    [Fact]
    public async Task Consolidado_SemPeriodo_UsaOMesCorrente()
    {
        var consolidado = await CriarServico().ObterConsolidadoAsync(null, null);

        // O recorte do fechamento, e o que evita varrer a base inteira
        Assert.Equal(1, consolidado.PeriodoInicio.Day);
        Assert.Equal(DateTime.UtcNow.Month, consolidado.PeriodoInicio.Month);
    }

    [Fact]
    public async Task Consolidado_ComPeriodoInvertido_NaoEAceito()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CriarServico()
            .ObterConsolidadoAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(-10)));
    }

    [Fact]
    public async Task Consolidado_PorQuemNaoEAdmin_ERecusado()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterConsolidadoAsync(null, null));

        // O veterinario ve o proprio dinheiro pelo extrato (RN-024)
        Assert.Equal("RN-106", ex.Codigo);
    }

    // ── Liquidação (RN-071) ──────────────────────────────────────────────────

    [Fact]
    public async Task Liquidacao_MarcaOsRepassesDoPeriodo()
    {
        var primeiro = Pagamento();
        var segundo = Pagamento();
        NoPeriodo(primeiro, segundo);

        var resultado = await CriarServico().LiquidarAsync(PedidoDeLiquidacao());

        Assert.Equal(2, resultado.PagamentosLiquidados);
        Assert.Equal(352m, resultado.ValorLiquidado);
        Assert.True(primeiro.Liquidado);
        Assert.True(segundo.Liquidado);
    }

    [Fact]
    public async Task Liquidacao_RepetidaNaoPagaDuasVezes()
    {
        NoPeriodo(Pagamento(liquidado: true), Pagamento(liquidado: true));

        var resultado = await CriarServico().LiquidarAsync(PedidoDeLiquidacao());

        // A operacao repete fechamento com frequencia
        Assert.Equal(0, resultado.PagamentosLiquidados);
        Assert.Equal(2, resultado.JaEstavamLiquidados);
        Assert.Equal(0m, resultado.ValorLiquidado);
    }

    [Fact]
    public async Task Liquidacao_DeUmDestinatario_NaoTocaNosOutros()
    {
        var doVet = Pagamento();
        var deOutro = Pagamento(destinatario: Guid.NewGuid());
        NoPeriodo(doVet, deOutro);

        var resultado = await CriarServico().LiquidarAsync(PedidoDeLiquidacao(_vet.Id));

        Assert.Equal(1, resultado.PagamentosLiquidados);
        Assert.True(doVet.Liquidado);
        Assert.False(deOutro.Liquidado);
    }

    [Fact]
    public async Task Liquidacao_GuardaAReferenciaDoPagamentoExterno()
    {
        NoPeriodo(Pagamento());

        var resultado = await CriarServico().LiquidarAsync(PedidoDeLiquidacao());

        // Marcar como pago sem dizer com base em que deixa a conferencia sem ancora
        Assert.Equal("TED-2026-08-001", resultado.Referencia);
        Assert.NotEqual(default, resultado.RealizadaEm);
    }

    [Fact]
    public async Task Liquidacao_SemNadaALiquidar_NaoSalva()
    {
        var resultado = await CriarServico().LiquidarAsync(PedidoDeLiquidacao());

        Assert.Equal(0, resultado.PagamentosLiquidados);
        _repo.Verify(r => r.SalvarAsync(), Times.Never);
    }

    [Fact]
    public async Task Liquidacao_PorQuemNaoEAdmin_ERecusada()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);

        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().LiquidarAsync(PedidoDeLiquidacao()));
    }

    [Fact]
    public async Task Liquidacao_ComPeriodoInvertido_NaoEAceita()
    {
        var pedido = PedidoDeLiquidacao();
        (pedido.Inicio, pedido.Fim) = (pedido.Fim, pedido.Inicio);

        await Assert.ThrowsAsync<ValidationException>(() => CriarServico().LiquidarAsync(pedido));
    }

    [Fact]
    public void Liquidacao_DePagamentoNaoConfirmado_NaoEPermitida()
    {
        var pendente = new Pagamento(Guid.NewGuid(), 200m, MeioPagamento.Pix, Guid.NewGuid());

        // Marcar como pago um repasse cuja cobranca nao se confirmou faria o extrato do
        // profissional mentir justamente no numero que ele vem conferir (RN-024)
        Assert.Throws<InvalidOperationException>(() => pendente.Liquidar());
    }
}
