using Vetly.Application.Strategies.Cancelamento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios das 3 strategies de cancelamento.
/// Valida as fronteiras de tempo (>24h, 2-24h, menos de 2h) exatamente como definido nas RN-019/020/021.
/// </summary>
public class CancelamentoStrategyTests
{
    private static Pagamento CriarPagamento(decimal valor = 200m)
    {
        var crmv = new Crmv("12345-SP");
        var vet = new Veterinario("Vet Teste", crmv, "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        return new Pagamento(Guid.NewGuid(), valor, MeioPagamento.Pix, Guid.NewGuid(), null);
    }

    // ── ReembolsoIntegralStrategy ─────────────────────────────────────────────

    [Fact]
    public void ReembolsoIntegral_Aplicavel_QuandoMaisDe24Horas()
    {
        var strategy = new ReembolsoIntegralStrategy();
        var horarioConsulta = DateTime.UtcNow.AddHours(25);
        var horarioCancelamento = DateTime.UtcNow;

        Assert.True(strategy.Aplicavel(horarioConsulta, horarioCancelamento));
    }

    [Fact]
    public void ReembolsoIntegral_NaoAplicavel_QuandoExatamente24Horas()
    {
        var strategy = new ReembolsoIntegralStrategy();
        var horarioConsulta = DateTime.UtcNow.AddHours(24);
        var horarioCancelamento = DateTime.UtcNow;

        // Fronteira: exatamente 24h nao aplica (requer > 24h)
        Assert.False(strategy.Aplicavel(horarioConsulta, horarioCancelamento));
    }

    [Fact]
    public void ReembolsoIntegral_Executar_RetornaReembolsoTotal()
    {
        var strategy = new ReembolsoIntegralStrategy();
        var pagamento = CriarPagamento(200m);

        var resultado = strategy.Executar(pagamento, percentualRetencao: 30m);

        Assert.Equal(200m, resultado.ValorReembolso);
        Assert.Equal(0m, resultado.PercentualRetencao);
        Assert.Equal("Reembolso Integral", resultado.EstrategiaAplicada);
    }

    // ── ReembolsoParcialStrategy ──────────────────────────────────────────────

    [Fact]
    public void ReembolsoParcial_Aplicavel_QuandoEntre2E24Horas()
    {
        var strategy = new ReembolsoParcialStrategy();
        var horarioConsulta = DateTime.UtcNow.AddHours(12);
        var horarioCancelamento = DateTime.UtcNow;

        Assert.True(strategy.Aplicavel(horarioConsulta, horarioCancelamento));
    }

    [Fact]
    public void ReembolsoParcial_Aplicavel_FronteiraSuperior_Exatamente24Horas()
    {
        var strategy = new ReembolsoParcialStrategy();
        var horarioConsulta = DateTime.UtcNow.AddHours(24);
        var horarioCancelamento = DateTime.UtcNow;

        // Fronteira: 24h inclusive aplica no parcial (2 < x <= 24)
        Assert.True(strategy.Aplicavel(horarioConsulta, horarioCancelamento));
    }

    [Fact]
    public void ReembolsoParcial_NaoAplicavel_QuandoMenos2Horas()
    {
        var strategy = new ReembolsoParcialStrategy();
        var horarioConsulta = DateTime.UtcNow.AddHours(1);
        var horarioCancelamento = DateTime.UtcNow;

        Assert.False(strategy.Aplicavel(horarioConsulta, horarioCancelamento));
    }

    [Fact]
    public void ReembolsoParcial_Executar_RetornaValorCorreto_Com30PorCentoRetencao()
    {
        var strategy = new ReembolsoParcialStrategy();
        var pagamento = CriarPagamento(200m);

        var resultado = strategy.Executar(pagamento, percentualRetencao: 30m);

        Assert.Equal(140m, resultado.ValorReembolso); // 200 - (200 * 0.30) = 140
        Assert.Equal(30m, resultado.PercentualRetencao);
        Assert.Equal("Reembolso Parcial", resultado.EstrategiaAplicada);
    }

    // ── SemReembolsoStrategy ──────────────────────────────────────────────────

    [Fact]
    public void SemReembolso_Aplicavel_QuandoMenosDe2Horas()
    {
        var strategy = new SemReembolsoStrategy();
        var horarioConsulta = DateTime.UtcNow.AddMinutes(30);
        var horarioCancelamento = DateTime.UtcNow;

        Assert.True(strategy.Aplicavel(horarioConsulta, horarioCancelamento));
    }

    [Fact]
    public void SemReembolso_Aplicavel_QuandoAposHorario()
    {
        var strategy = new SemReembolsoStrategy();
        // Consulta ja passou
        var horarioConsulta = DateTime.UtcNow.AddHours(-1);
        var horarioCancelamento = DateTime.UtcNow;

        Assert.True(strategy.Aplicavel(horarioConsulta, horarioCancelamento));
    }

    [Fact]
    public void SemReembolso_Executar_RetornaZeroReembolso()
    {
        var strategy = new SemReembolsoStrategy();
        var pagamento = CriarPagamento(200m);

        var resultado = strategy.Executar(pagamento, percentualRetencao: 30m);

        Assert.Equal(0m, resultado.ValorReembolso);
        Assert.Equal(100m, resultado.PercentualRetencao);
        Assert.Equal("Sem Reembolso", resultado.EstrategiaAplicada);
    }

    // ── Prioridades ───────────────────────────────────────────────────────────

    [Fact]
    public void Strategies_Prioridades_SaoCorretas()
    {
        Assert.Equal(1, new ReembolsoIntegralStrategy().Prioridade);
        Assert.Equal(2, new ReembolsoParcialStrategy().Prioridade);
        Assert.Equal(3, new SemReembolsoStrategy().Prioridade);
    }
}
