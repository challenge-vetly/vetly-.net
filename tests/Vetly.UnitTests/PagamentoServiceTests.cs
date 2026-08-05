using Moq;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Application.Strategies.Comissao;
using Vetly.Application.Strategies.Split;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do PagamentoService.
/// Cobre a seleção de Strategy de split financeiro (Autônomo 80% / Vinculado 60%),
/// a regra PAGAMENTO-001 (split requer ConsultaId) e a comissão por plano via
/// IComissaoStrategy (Básico 15% / Profissional 12% / Enterprise 10% — RN-089).
/// </summary>
public class PagamentoServiceTests
{
    private readonly Mock<IPagamentoRepository> _repoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private static readonly IComissaoStrategy[] TodasAsComissoes =
        [new ComissaoBasicoStrategy(), new ComissaoProfissionalStrategy(), new ComissaoEnterpriseStrategy()];

    private PagamentoService CriarServico(params ISplitFinanceiroStrategy[] splitStrategies) =>
        new(_repoMock.Object, _vetRepoMock.Object, _consultaRepoMock.Object, splitStrategies, TodasAsComissoes, TimeProvider.System);

    private static Veterinario CriarVet(PersonaVeterinario persona, PlanoAssinatura plano) =>
        new("Dr. Vet", new Crmv("12345-SP"), "SP", persona, plano);

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

        var vet = CriarVet(PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            vet.Id, Guid.NewGuid(), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consultaId)).ReturnsAsync(consulta);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Pagamento>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico(new SplitAutonomoStrategy(), new SplitEmpresaStrategy())
            .ProcessarSplitAsync(pagamento.Id);

        Assert.Equal(80m, resultado.PercentualSplit);
        Assert.Equal(12m, resultado.PercentualComissao); // Profissional — ProcessarSplitAsync tambem aplica comissao
    }

    [Fact]
    public async Task ProcessarSplitAsync_VetVinculado_AplicaSplitEmpresa60Porcento()
    {
        var consultaId = Guid.NewGuid();
        var pagamento = new Pagamento(Guid.NewGuid(), 300m, MeioPagamento.Pix, consultaId);
        pagamento.Confirmar();

        var vet = CriarVet(PersonaVeterinario.Vinculado, PlanoAssinatura.Profissional);
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
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

    [Fact]
    public async Task ProcessarSimuladoAsync_ExemploCanonico_Consulta150ReaisVetProfissional_ComissaoR18RepasseR132()
    {
        var vet = CriarVet(PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            vet.Id, Guid.NewGuid(), Guid.NewGuid());
        consulta.IniciarCheckout(DateTime.UtcNow);

        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _consultaRepoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _consultaRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = new SimularPagamentoDto { ConsultaId = consulta.Id, Valor = 150.00m, Meio = MeioPagamento.Pix };
        var resultado = await CriarServico().ProcessarSimuladoAsync(dto);

        Assert.True(resultado.Simulado);
        Assert.Equal(StatusPagamento.Confirmado, resultado.Status);
        Assert.Equal(12m, resultado.PercentualComissao);
        Assert.Equal(18.00m, resultado.ValorComissao);
        Assert.Equal(132.00m, resultado.ValorRepasse);
        Assert.Equal(StatusConsulta.Confirmada, resultado.ConsultaStatus);
    }

    [Theory]
    [InlineData(PlanoAssinatura.Basico, 15)]
    [InlineData(PlanoAssinatura.Profissional, 12)]
    [InlineData(PlanoAssinatura.Enterprise, 10)]
    public async Task ProcessarSimuladoAsync_PorPlano_AplicaPercentualDeComissaoCorreto(PlanoAssinatura plano, decimal percentualEsperado)
    {
        var vet = CriarVet(PersonaVeterinario.Autonomo, plano);
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            vet.Id, Guid.NewGuid(), Guid.NewGuid());
        consulta.IniciarCheckout(DateTime.UtcNow);

        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _consultaRepoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _consultaRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = new SimularPagamentoDto { ConsultaId = consulta.Id, Valor = 100.00m, Meio = MeioPagamento.Pix };
        var resultado = await CriarServico().ProcessarSimuladoAsync(dto);

        Assert.Equal(percentualEsperado, resultado.PercentualComissao);
    }
}
