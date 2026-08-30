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
/// Cobre RN-006 (agendamento requer pagamento confirmado), RN-039 (modalidade remota fora de
/// escopo) e seleção de Strategy de cancelamento.
/// </summary>
public class ConsultaServiceTests
{
    private readonly Mock<IConsultaRepository> _repoMock = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepoMock = new();
    private readonly Mock<IDocumentoRepository> _documentoRepoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IEmpresaRepository> _empresaRepoMock = new();
    private readonly Mock<IUsuarioAtual> _usuarioMock = new();

    private ConsultaService CriarServico(params ICancelamentoStrategy[] strategies) =>
        new(_repoMock.Object, _pagamentoRepoMock.Object, _documentoRepoMock.Object,
            _animalRepoMock.Object, _vetRepoMock.Object, _empresaRepoMock.Object,
            strategies, _usuarioMock.Object);

    /// <summary>Por padrao os testes rodam como Admin, que enxerga todo o escopo.</summary>
    public ConsultaServiceTests() => _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

    /// <summary>
    /// Prepara um veterinario vinculado a uma clinica com a politica de retencao informada,
    /// e devolve o id do veterinario para montar a consulta (RN-042).
    /// </summary>
    private Guid VetVinculadoAClinicaComRetencao(decimal percentual)
    {
        var empresa = new Empresa("Clinica Vida Pet", "Clinica", Guid.NewGuid());
        empresa.DefinirPoliticaRetencao(percentual);

        var vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Vinculado, PlanoAssinatura.Enterprise);
        vet.VincularEmpresa(empresa.Id);

        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _empresaRepoMock.Setup(r => r.ObterPorIdAsync(empresa.Id)).ReturnsAsync(empresa);
        return vet.Id;
    }

    private static CriarConsultaDto CriarDto(Guid pagamentoId) => new()
    {
        DataHora = DateTime.UtcNow.AddDays(1),
        Modalidade = ModalidadeAtendimento.Presencial,
        VeterinarioId = Guid.NewGuid(),
        AnimalId = Guid.NewGuid(),
        TutorId = Guid.NewGuid(),
        PagamentoId = pagamentoId
    };

    [Fact]
    public async Task AgendarAsync_PagamentoConfirmado_RetornaConsultaDto()
    {
        var pagamentoId = Guid.NewGuid();
        // Pagamento sem consultaId — cenário real: tutor paga antes de agendar
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

        Assert.Equal("RN-006", ex.Codigo);
    }

    [Fact]
    public async Task AgendarAsync_ModalidadeRemota_LancaBusinessRuleExceptionRN039()
    {
        var dto = CriarDto(Guid.NewGuid());
        dto.Modalidade = ModalidadeAtendimento.Remoto;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().AgendarAsync(dto));

        Assert.Equal("RN-039", ex.Codigo);
        // A guarda roda antes de qualquer efeito colateral: o pagamento nem chega a ser lido
        _pagamentoRepoMock.Verify(r => r.ObterPorIdAsync(It.IsAny<Guid>()), Times.Never);
        _repoMock.Verify(r => r.AdicionarAsync(It.IsAny<Consulta>()), Times.Never);
    }

    [Fact]
    public async Task AtualizarAsync_ModalidadeRemota_LancaBusinessRuleExceptionRN039()
    {
        var dto = CriarDto(Guid.NewGuid());
        dto.Modalidade = ModalidadeAtendimento.Remoto;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().AtualizarAsync(Guid.NewGuid(), dto));

        Assert.Equal("RN-039", ex.Codigo);
        _repoMock.Verify(r => r.Atualizar(It.IsAny<Consulta>()), Times.Never);
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

    // ── Máquina de estados da consulta (RN-035/RN-038) ───────────────────────

    private static Consulta NovaConsulta() => new(
        DateTime.UtcNow.AddHours(10), ModalidadeAtendimento.Presencial,
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Consulta_NasceEmCheckout()
    {
        var consulta = NovaConsulta();

        Assert.Equal(StatusConsulta.EmCheckout, consulta.Status);
        Assert.Equal(StatusPagamento.Pendente, consulta.StatusPagamento);
    }

    [Fact]
    public void ConfirmarPagamento_PromoveDeCheckoutParaConfirmada()
    {
        var consulta = NovaConsulta();

        consulta.ConfirmarPagamento();

        Assert.Equal(StatusConsulta.Confirmada, consulta.Status);
    }

    [Fact]
    public void ConfirmarPagamento_EmConsultaCancelada_NaoAReabre()
    {
        var consulta = NovaConsulta();
        consulta.Cancelar();

        consulta.ConfirmarPagamento();

        Assert.Equal(StatusConsulta.Cancelada, consulta.Status);
    }

    [Fact]
    public void Cancelar_EFinalizar_MantemOsBooleanosEmDuplaEscrita()
    {
        var cancelada = NovaConsulta();
        cancelada.Cancelar();

        var realizada = NovaConsulta();
        realizada.ConfirmarPagamento();
        realizada.Finalizar();

        // Enquanto durar a dupla escrita, STATUS e os booleanos nao podem divergir
        Assert.Equal(StatusConsulta.Cancelada, cancelada.Status);
        Assert.True(cancelada.Cancelada);

        Assert.Equal(StatusConsulta.Realizada, realizada.Status);
        Assert.True(realizada.Finalizada);
    }

    [Fact]
    public void RegistrarNoShow_EDistintoDeCancelamento()
    {
        var consulta = NovaConsulta();
        consulta.ConfirmarPagamento();

        consulta.RegistrarNoShow();

        // O que os tres booleanos nao conseguiam expressar: no-show nao e cancelamento (RN-044)
        Assert.Equal(StatusConsulta.NoShow, consulta.Status);
        Assert.False(consulta.Cancelada);
    }

    [Fact]
    public void Expirar_MarcaConsultaCujoLockVenceu()
    {
        var consulta = NovaConsulta();

        consulta.Expirar();

        Assert.Equal(StatusConsulta.Expirada, consulta.Status);
    }

    // ── Retenção configurável pela clínica (RN-042, C-06) ────────────────────

    [Theory]
    [InlineData(15, 170)]   // clinica retem 15% de R$ 200 => reembolsa 170
    [InlineData(50, 100)]
    [InlineData(0, 200)]    // clinica que nao retem nada devolve integral mesmo na faixa parcial
    public async Task CancelarAsync_FaixaParcial_UsaOPercentualDaClinica(
        decimal percentualDaClinica, decimal reembolsoEsperado)
    {
        var veterinarioId = VetVinculadoAClinicaComRetencao(percentualDaClinica);

        var consulta = new Consulta(
            DateTime.UtcNow.AddHours(10), ModalidadeAtendimento.Presencial,
            veterinarioId, Guid.NewGuid(), Guid.NewGuid());
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

        Assert.Equal("Reembolso Parcial", resultado.EstrategiaAplicada);
        Assert.Equal(percentualDaClinica, resultado.PercentualRetencao);
        Assert.Equal(reembolsoEsperado, resultado.ValorReembolso);
    }

    [Fact]
    public async Task CancelarAsync_VetAutonomoSemEmpresa_CaiNoPadraoDeTrintaPorCento()
    {
        var vet = new Veterinario("Dr. Autonomo", new Crmv("54321-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Basico);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);

        var consulta = new Consulta(
            DateTime.UtcNow.AddHours(10), ModalidadeAtendimento.Presencial,
            vet.Id, Guid.NewGuid(), Guid.NewGuid());
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

        Assert.Equal(30m, resultado.PercentualRetencao);
        Assert.Equal(140m, resultado.ValorReembolso);
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

        Assert.Equal("RN-087", ex.Codigo);
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

        Assert.Equal("RN-087", ex.Codigo);
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
