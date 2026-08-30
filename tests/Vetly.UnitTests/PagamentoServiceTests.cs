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

    private PagamentoService CriarServico(params ISplitFinanceiroStrategy[] strategies) =>
        new(_repoMock.Object, _vetRepoMock.Object, _consultaRepoMock.Object, strategies, _usuarioMock.Object);

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

    [Fact]
    public async Task ProcessarSplitAsync_VetAutonomo_AplicaSplitAutonomo80Porcento()
    {
        var consultaId = Guid.NewGuid();
        var pagamento = new Pagamento(Guid.NewGuid(), 300m, MeioPagamento.Pix, consultaId);
        pagamento.Confirmar();

        var crmv = new Crmv("12345-SP");
        var vet = new Veterinario("Dr. Autonomo", crmv, "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            vet.Id, Guid.NewGuid(), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consultaId)).ReturnsAsync(consulta);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Pagamento>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico(new SplitAutonomoStrategy(), new SplitEmpresaStrategy())
            .ProcessarSplitAsync(pagamento.Id);

        Assert.Equal(80m, resultado.PercentualSplit);
    }

    [Fact]
    public async Task ProcessarSplitAsync_VetVinculado_AplicaSplitEmpresa60Porcento()
    {
        var consultaId = Guid.NewGuid();
        var pagamento = new Pagamento(Guid.NewGuid(), 300m, MeioPagamento.Pix, consultaId);
        pagamento.Confirmar();

        var crmv = new Crmv("12345-SP");
        var vet = new Veterinario("Dr. Vinculado", crmv, "SP", PersonaVeterinario.Vinculado, PlanoAssinatura.Profissional);
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            vet.Id, Guid.NewGuid(), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consultaId)).ReturnsAsync(consulta);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Pagamento>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico(new SplitAutonomoStrategy(), new SplitEmpresaStrategy())
            .ProcessarSplitAsync(pagamento.Id);

        Assert.Equal(60m, resultado.PercentualSplit);
    }
}
