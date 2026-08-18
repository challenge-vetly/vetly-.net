using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do EmpresaService.
/// Cobre o recalculo de faixa Enterprise ao vincular vet (RN-092), a agregacao do
/// dashboard financeiro consolidado (RN-007) e a autorizacao por posse do Admin (RN-001..006).
/// </summary>
public class EmpresaServiceTests
{
    private readonly Mock<IEmpresaRepository> _repoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    private EmpresaService CriarServico() =>
        new(_repoMock.Object, _vetRepoMock.Object, _consultaRepoMock.Object, _pagamentoRepoMock.Object, _currentUserMock.Object);

    private static Empresa CriarEmpresa() => new("Clinica Teste", "Clinica", Guid.NewGuid());

    private static Veterinario CriarVet() =>
        new("Dr. Vet", new Crmv("12345-SP"), "SP", PersonaVeterinario.Vinculado, PlanoAssinatura.Enterprise);

    [Fact]
    public async Task VincularVeterinarioAsync_EmpresaPassaDeCincoParaSeisVets_RecalculaParaProximoDegrau()
    {
        var empresa = CriarEmpresa();
        var vet = CriarVet();
        var outrosCincoVets = Enumerable.Range(0, 5).Select(_ => CriarVet()).ToList();

        _repoMock.Setup(r => r.ObterPorIdAsync(empresa.Id)).ReturnsAsync(empresa);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _vetRepoMock.Setup(r => r.Atualizar(vet));
        _vetRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepoMock.Setup(r => r.ObterPorEmpresaAsync(empresa.Id)).ReturnsAsync([.. outrosCincoVets, vet]); // 6 no total
        _repoMock.Setup(r => r.Atualizar(empresa));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        await CriarServico().VincularVeterinarioAsync(empresa.Id, vet.Id);

        Assert.Equal(999m, empresa.FaixaEnterprise); // 6 vets -> faixa 6-10
    }

    [Fact]
    public async Task ObterDashboardConsolidadoAsync_EmpresaNaoEncontrada_LancaNotFoundException()
    {
        _repoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Empresa?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().ObterDashboardConsolidadoAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ObterDashboardConsolidadoAsync_AdminDeOutraEmpresa_LancaForbiddenExceptionACESSO002()
    {
        var empresa = CriarEmpresa();
        _repoMock.Setup(r => r.ObterPorIdAsync(empresa.Id)).ReturnsAsync(empresa);
        _currentUserMock.Setup(c => c.Role).Returns("Admin");
        _currentUserMock.Setup(c => c.EntidadeId).Returns(Guid.NewGuid()); // empresa diferente

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => CriarServico().ObterDashboardConsolidadoAsync(empresa.Id));

        Assert.Equal("ACESSO-002", ex.Codigo);
    }

    [Fact]
    public async Task ObterDashboardConsolidadoAsync_AdminDaPropriaEmpresa_AgregaFaturamentoComissaoRepasseEReembolso()
    {
        var empresa = CriarEmpresa();
        var vet1 = CriarVet();
        var vet2 = CriarVet();

        var c1 = new Consulta(DateTime.UtcNow.AddDays(-5), ModalidadeAtendimento.Presencial, TipoServico.Consulta, vet1.Id, Guid.NewGuid(), Guid.NewGuid());
        c1.IniciarCheckout(DateTime.UtcNow.AddDays(-5));
        c1.ConfirmarPagamento(DateTime.UtcNow.AddDays(-5));
        c1.MarcarRealizada(DateTime.UtcNow.AddDays(-4));

        var c2 = new Consulta(DateTime.UtcNow.AddDays(-3), ModalidadeAtendimento.Presencial, TipoServico.Consulta, vet1.Id, Guid.NewGuid(), Guid.NewGuid());
        c2.IniciarCheckout(DateTime.UtcNow.AddDays(-3));
        c2.Cancelar();

        var c3 = new Consulta(DateTime.UtcNow.AddDays(-2), ModalidadeAtendimento.Presencial, TipoServico.Consulta, vet2.Id, Guid.NewGuid(), Guid.NewGuid());
        c3.IniciarCheckout(DateTime.UtcNow.AddDays(-2));
        c3.ConfirmarPagamento(DateTime.UtcNow.AddDays(-2));
        c3.MarcarRealizada(DateTime.UtcNow.AddDays(-1));

        var p1 = new Pagamento(Guid.NewGuid(), 150m, MeioPagamento.Pix, c1.Id);
        p1.RegistrarComissao(12m); // ValorComissao=18, ValorRepasse=132

        var p2 = new Pagamento(Guid.NewGuid(), 100m, MeioPagamento.Pix, c2.Id);
        p2.RegistrarComissao(15m); // ValorComissao=15, ValorRepasse=85
        p2.Estornar(50m);

        _repoMock.Setup(r => r.ObterPorIdAsync(empresa.Id)).ReturnsAsync(empresa);
        _repoMock.Setup(r => r.Atualizar(empresa));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepoMock.Setup(r => r.ObterPorEmpresaAsync(empresa.Id)).ReturnsAsync([vet1, vet2]);
        _consultaRepoMock.Setup(r => r.ObterPorVeterinariosAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([c1, c2, c3]);
        _pagamentoRepoMock.Setup(r => r.ObterPorVeterinariosAsync(It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([p1, p2]);

        var resultado = await CriarServico().ObterDashboardConsolidadoAsync(empresa.Id);

        Assert.Equal(2, resultado.QtdVeterinariosAtivos);
        Assert.Equal(250m, resultado.FaturamentoBruto);
        Assert.Equal(33m, resultado.TotalComissoes);
        Assert.Equal(217m, resultado.TotalRepasses);
        Assert.Equal(50m, resultado.TotalReembolsos);
        Assert.Equal(2, resultado.QtdConsultasRealizadas);
        Assert.Equal(1, resultado.QtdConsultasCanceladas);
        Assert.Equal(599m, resultado.FaixaEnterprise); // 2 vets -> faixa base
    }

    [Fact]
    public async Task ObterAssinaturaAsync_RetornaFaixaRecalculadaPelaContagemAtualDeVets()
    {
        var empresa = CriarEmpresa();
        var vets = Enumerable.Range(0, 12).Select(_ => CriarVet()).ToList();

        _repoMock.Setup(r => r.ObterPorIdAsync(empresa.Id)).ReturnsAsync(empresa);
        _repoMock.Setup(r => r.Atualizar(empresa));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepoMock.Setup(r => r.ObterPorEmpresaAsync(empresa.Id)).ReturnsAsync(vets);

        var resultado = await CriarServico().ObterAssinaturaAsync(empresa.Id);

        Assert.Equal(12, resultado.QtdVeterinariosAtivos);
        Assert.Equal(1699m, resultado.FaixaEnterprise); // 11-20 vets
    }
}
