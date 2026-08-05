using Moq;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Application.Strategies.Cancelamento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do ConsultaService.
/// Cobre RN-015 (agendamento requer pagamento confirmado) e seleção de Strategy de cancelamento.
/// </summary>
public class ConsultaServiceTests
{
    private readonly Mock<IConsultaRepository> _repoMock = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepoMock = new();
    private readonly Mock<IDocumentoRepository> _documentoRepoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();

    private ConsultaService CriarServico(params ICancelamentoStrategy[] strategies) =>
        new(_repoMock.Object, _pagamentoRepoMock.Object, _documentoRepoMock.Object, _animalRepoMock.Object, strategies);

    private static CriarConsultaDto CriarDto(Guid pagamentoId) => new()
    {
        DataHora = DateTime.UtcNow.AddDays(1),
        Modalidade = ModalidadeAtendimento.Presencial,
        VeterinarioId = Guid.NewGuid(),
        AnimalId = Guid.NewGuid(),
        ResponsavelId = Guid.NewGuid(),
        PagamentoId = pagamentoId
    };

    [Fact]
    public async Task AgendarAsync_PagamentoConfirmado_RetornaConsultaDto()
    {
        var pagamentoId = Guid.NewGuid();
        // Pagamento sem consultaId — cenário real: responsavel paga antes de agendar
        var pagamento = new Pagamento(Guid.NewGuid(), 200m, MeioPagamento.Pix);
        pagamento.Confirmar();

        _pagamentoRepoMock.Setup(r => r.ObterPorIdAsync(pagamentoId)).ReturnsAsync(pagamento);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Consulta>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _pagamentoRepoMock.Setup(r => r.Atualizar(It.IsAny<Pagamento>()));
        _pagamentoRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().AgendarAsync(CriarDto(pagamentoId));

        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal(StatusPagamento.Confirmado, resultado.StatusPagamento);
    }

    [Fact]
    public async Task AgendarAsync_PagamentoPendente_LancaBusinessRuleExceptionRN015()
    {
        var pagamentoId = Guid.NewGuid();
        // Pagamento criado sem Confirmar() — permanece Pendente
        var pagamento = new Pagamento(Guid.NewGuid(), 200m, MeioPagamento.Pix);

        _pagamentoRepoMock.Setup(r => r.ObterPorIdAsync(pagamentoId)).ReturnsAsync(pagamento);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().AgendarAsync(CriarDto(pagamentoId)));

        Assert.Equal("RN-015", ex.Codigo);
    }

    [Fact]
    public async Task CancelarAsync_ConsultaJaCancelada_LancaBusinessRuleExceptionCONSULTA001()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddHours(25), ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.Cancelar();

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().CancelarAsync(consulta.Id));

        Assert.Equal("CONSULTA-001", ex.Codigo);
    }

    [Fact]
    public async Task CancelarAsync_Antecedencia25h_UsaReembolsoIntegralStrategy()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddHours(25), ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var pagamento = new Pagamento(Guid.NewGuid(), 200m, MeioPagamento.Pix, consulta.Id);
        pagamento.Confirmar();

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _pagamentoRepoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(pagamento);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _pagamentoRepoMock.Setup(r => r.Atualizar(It.IsAny<Pagamento>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var service = CriarServico(
            new ReembolsoIntegralStrategy(),
            new ReembolsoParcialStrategy(),
            new SemReembolsoStrategy());

        var resultado = await service.CancelarAsync(consulta.Id);

        Assert.Equal("Reembolso Integral", resultado.EstrategiaAplicada);
        Assert.Equal(200m, resultado.ValorReembolso);
    }

    [Fact]
    public async Task FinalizarAsync_ComReceitaAssinada_Sucesso()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var receita = new Documento(TipoDocumento.ReceitaVeterinaria, "12345-SP", consulta.Id);
        receita.Assinar();

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _documentoRepoMock.Setup(r => r.ObterPorConsultaETipoAsync(consulta.Id, TipoDocumento.ReceitaVeterinaria))
            .ReturnsAsync(receita);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        await CriarServico().FinalizarAsync(consulta.Id);

        Assert.True(consulta.Finalizada);
    }

    [Fact]
    public async Task FinalizarAsync_SemReceita_LancaBusinessRuleExceptionRN031()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _documentoRepoMock.Setup(r => r.ObterPorConsultaETipoAsync(consulta.Id, TipoDocumento.ReceitaVeterinaria))
            .ReturnsAsync((Documento?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().FinalizarAsync(consulta.Id));

        Assert.Equal("RN-031", ex.Codigo);
    }

    [Fact]
    public async Task FinalizarAsync_ReceitaNaoAssinada_LancaBusinessRuleExceptionRN031()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var receita = new Documento(TipoDocumento.ReceitaVeterinaria, "12345-SP", consulta.Id);
        // NÃO chama receita.Assinar()

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _documentoRepoMock.Setup(r => r.ObterPorConsultaETipoAsync(consulta.Id, TipoDocumento.ReceitaVeterinaria))
            .ReturnsAsync(receita);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().FinalizarAsync(consulta.Id));

        Assert.Equal("RN-031", ex.Codigo);
    }

    [Fact]
    public async Task AgendarAsync_PagamentoConfirmado_VinculaPagamentoAConsulta()
    {
        var pagamentoId = Guid.NewGuid();
        var pagamento = new Pagamento(Guid.NewGuid(), 200m, MeioPagamento.Pix);
        pagamento.Confirmar();

        _pagamentoRepoMock.Setup(r => r.ObterPorIdAsync(pagamentoId)).ReturnsAsync(pagamento);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Consulta>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _pagamentoRepoMock.Setup(r => r.Atualizar(It.IsAny<Pagamento>()));
        _pagamentoRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().AgendarAsync(CriarDto(pagamentoId));

        Assert.Equal(resultado.Id, pagamento.ConsultaId);
        _pagamentoRepoMock.Verify(r => r.Atualizar(pagamento), Times.Once);
        _pagamentoRepoMock.Verify(r => r.SalvarAsync(), Times.Once);
    }

    [Fact]
    public async Task ValidarDiagnosticoAsync_ConsultaExistente_MarcaDiagnosticoValidado()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        await CriarServico().ValidarDiagnosticoAsync(consulta.Id);

        Assert.True(consulta.DiagnosticoValidado);
    }

    [Fact]
    public async Task ValidarDiagnosticoAsync_ConsultaCancelada_LancaBusinessRuleExceptionCONSULTA003()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.Cancelar();

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ValidarDiagnosticoAsync(consulta.Id));

        Assert.Equal("CONSULTA-003", ex.Codigo);
    }
}
