using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Application.Strategies.Split;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do PagamentoService.
/// Cobre a seleção de Strategy de split financeiro (Autônomo 80% / Vinculado 60%)
/// e a regra PAGAMENTO-001 (split requer ConsultaId).
/// </summary>
public class PagamentoServiceTests
{
    private readonly Mock<IPagamentoRepository> _repoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private readonly Mock<IUsuarioAtual> _usuarioMock = new();
    private readonly Mock<IEmpresaRepository> _empresaRepoMock = new();

    private readonly Mock<IPagamentoAdapter> _adaptadorMock = new();
    private readonly Mock<IAgendaRepository> _agendaRepoMock = new();

    private PagamentoService CriarServico(params ISplitFinanceiroStrategy[] strategies) =>
        new(_repoMock.Object, _vetRepoMock.Object, _consultaRepoMock.Object,
            _empresaRepoMock.Object, _adaptadorMock.Object, _agendaRepoMock.Object,
            strategies, _usuarioMock.Object);

    /// <summary>Todas as strategies de plano, como o DI as registra (RN-070).</summary>
    private static ISplitFinanceiroStrategy[] TodasAsStrategies() =>
        [new SplitBasicoStrategy(), new SplitProfissionalStrategy(), new SplitEnterpriseStrategy()];

    /// <summary>Por padrao os testes rodam como Admin, que enxerga todo o escopo.</summary>
    public PagamentoServiceTests() => _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

    [Fact]
    public async Task ProcessarSplitAsync_SemConsultaId_LancaBusinessRuleExceptionPAGAMENTO001()
    {
        // Pagamento sem ConsultaId — não vinculado a consulta
        var pagamento = new Pagamento(Guid.NewGuid(), 300m, MeioPagamento.CartaoCredito);

        _repoMock.Setup(r => r.ObterPorIdAsync(pagamento.Id)).ReturnsAsync(pagamento);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ProcessarSplitAsync(pagamento.Id));

        Assert.Equal("PAGAMENTO-001", ex.Codigo);
    }

    /// <summary>Clinica montada pelo helper, quando o cenario tem vet vinculado.</summary>
    private Empresa? _clinica;

    /// <summary>Prepara pagamento, consulta e vet para o calculo do split.</summary>
    private Pagamento PrepararSplit(
        decimal valor, PlanoAssinatura planoDoVet, bool vinculado = false, PlanoAssinatura? planoDaEmpresa = null)
    {
        var consultaId = Guid.NewGuid();
        var pagamento = new Pagamento(Guid.NewGuid(), valor, MeioPagamento.Pix, consultaId);
        pagamento.Confirmar();

        var persona = vinculado ? PersonaVeterinario.Vinculado : PersonaVeterinario.Autonomo;
        var vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP", persona, planoDoVet);

        if (vinculado)
        {
            // A clinica e criada antes do vinculo, para que o id consultado e o id da
            // entidade devolvida pelo mock sejam o mesmo
            _clinica = new Empresa("Clinica Vida Pet", "Clinica", Guid.NewGuid(),
                planoDaEmpresa ?? PlanoAssinatura.Enterprise);
            vet.VincularEmpresa(_clinica.Id);
            _empresaRepoMock.Setup(r => r.ObterPorIdAsync(_clinica.Id)).ReturnsAsync(_clinica);
        }

        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            vet.Id, Guid.NewGuid(), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consultaId)).ReturnsAsync(consulta);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Pagamento>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        return pagamento;
    }

    // ── Take rate por plano (RN-070, C-01) ───────────────────────────────────

    [Theory]
    [InlineData(PlanoAssinatura.Basico, 15, 30, 170)]
    [InlineData(PlanoAssinatura.Profissional, 12, 24, 176)]
    [InlineData(PlanoAssinatura.Enterprise, 10, 20, 180)]
    public async Task ProcessarSplit_AplicaOTakeRateDoPlano(
        PlanoAssinatura plano, decimal takeRateEsperado, decimal comissaoEsperada, decimal repasseEsperado)
    {
        var pagamento = PrepararSplit(200m, plano);

        var resultado = await CriarServico(TodasAsStrategies()).ProcessarSplitAsync(pagamento.Id);

        // A maior comissao pertence ao menor plano: a escada troca assinatura por comissao
        Assert.Equal(plano, resultado.PlanoAplicado);
        Assert.Equal(takeRateEsperado, resultado.TakeRate);
        Assert.Equal(comissaoEsperada, resultado.Comissao);
        Assert.Equal(repasseEsperado, resultado.Repasse);
    }

    [Fact]
    public async Task ProcessarSplit_ComissaoERepasse_SempreFecham_OValorDaTransacao()
    {
        // 333,33 com 12% da uma comissao que nao e exata: o repasse vem por subtracao
        var pagamento = PrepararSplit(333.33m, PlanoAssinatura.Profissional);

        var resultado = await CriarServico(TodasAsStrategies()).ProcessarSplitAsync(pagamento.Id);

        Assert.Equal(333.33m, resultado.Comissao!.Value + resultado.Repasse!.Value);
        Assert.Equal(40.00m, resultado.Comissao);
    }

    [Fact]
    public async Task ProcessarSplit_ExemploFechadoDoProduto_Enterprise200Reais()
    {
        // Produto §9: clinica Enterprise, atendimento de R$ 200 => Vetly 20, unidade 180
        var pagamento = PrepararSplit(200m, PlanoAssinatura.Basico, vinculado: true, PlanoAssinatura.Enterprise);

        var resultado = await CriarServico(TodasAsStrategies()).ProcessarSplitAsync(pagamento.Id);

        Assert.Equal(20m, resultado.Comissao);
        Assert.Equal(180m, resultado.Repasse);
    }

    // ── Quem paga e quem recebe (RN-072) ─────────────────────────────────────

    [Fact]
    public async Task ProcessarSplit_VetVinculado_UsaOPlanoDaClinicaERepassaAEla()
    {
        // O vet e Basico, a clinica e Enterprise: quem assina e a unidade
        var pagamento = PrepararSplit(200m, PlanoAssinatura.Basico, vinculado: true, PlanoAssinatura.Enterprise);

        var resultado = await CriarServico(TodasAsStrategies()).ProcessarSplitAsync(pagamento.Id);

        Assert.Equal(PlanoAssinatura.Enterprise, resultado.PlanoAplicado);
        Assert.Equal(10m, resultado.TakeRate);
        // Repasse unico, para a unidade. A remuneracao interna do vinculado esta fora
        // do escopo da plataforma (RN-072)
        Assert.Equal(_clinica!.Id, resultado.DestinatarioRepasseId);
    }

    [Fact]
    public async Task ProcessarSplit_VetAutonomo_RepassaAEleMesmo()
    {
        var pagamento = PrepararSplit(200m, PlanoAssinatura.Profissional);

        var resultado = await CriarServico(TodasAsStrategies()).ProcessarSplitAsync(pagamento.Id);

        Assert.NotNull(resultado.DestinatarioRepasseId);
        Assert.Equal(PlanoAssinatura.Profissional, resultado.PlanoAplicado);
    }

    [Fact]
    public async Task ProcessarSplit_MantemOPercentualSplitAntigoCoerente()
    {
        var pagamento = PrepararSplit(200m, PlanoAssinatura.Basico);

        var resultado = await CriarServico(TodasAsStrategies()).ProcessarSplitAsync(pagamento.Id);

        // A coluna antiga continua alimentada: e o percentual que fica com o prestador
        Assert.Equal(85m, resultado.PercentualSplit);
    }

    // ── Strategies isoladas ──────────────────────────────────────────────────

    [Fact]
    public void Strategy_SoSeAplicaAoProprioPlano()
    {
        var basico = new SplitBasicoStrategy();

        Assert.True(basico.Aplicavel(PlanoAssinatura.Basico));
        Assert.False(basico.Aplicavel(PlanoAssinatura.Enterprise));
    }

    [Fact]
    public void Strategy_ValorNegativo_NaoEAceito()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SplitBasicoStrategy().Calcular(-1m));
    }

    [Fact]
    public void RegistrarSplit_QueNaoFechaOValor_NaoEAceito()
    {
        var pagamento = new Pagamento(Guid.NewGuid(), 200m, MeioPagamento.Pix, Guid.NewGuid());

        // Salvaguarda contra centavo perdido no arredondamento
        Assert.Throws<ArgumentException>(() =>
            pagamento.RegistrarSplit(PlanoAssinatura.Basico, 15m, 30m, 100m, Guid.NewGuid()));
    }
}
