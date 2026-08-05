using Moq;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Application.Strategies.Cancelamento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do ConsultaService.
/// Cobre LGPD-001 (consentimento clinico), RN-057 (modalidade presencial), a maquina de
/// estados orquestrada pelo service (confirmar pagamento, marcar realizada com posse do
/// vet, no-show, remarcar) e selecao de Strategy de cancelamento (RN-019/020/021).
/// </summary>
public class ConsultaServiceTests
{
    private readonly Mock<IConsultaRepository> _repoMock = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepoMock = new();
    private readonly Mock<IDocumentoRepository> _documentoRepoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private readonly Mock<IConsentimentoLgpdRepository> _consentimentoRepoMock = new();
    private readonly Mock<IResponsavelRepository> _responsavelRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IAcessoProntuarioService> _acessoProntuarioServiceMock = new();
    private readonly Mock<IAvaliacaoService> _avaliacaoServiceMock = new();
    private readonly Mock<IFidelidadeService> _fidelidadeServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    public ConsultaServiceTests()
    {
        // Por padrão, todo responsavel tem o consentimento clinico ativo (LGPD-001) —
        // os testes que precisam do cenário contrário sobrescrevem este setup.
        _consentimentoRepoMock
            .Setup(r => r.ObterAtivoAsync(It.IsAny<Guid>(), FinalidadeConsentimento.AtendimentoClinico))
            .ReturnsAsync(new ConsentimentoLgpd(Guid.NewGuid(), FinalidadeConsentimento.AtendimentoClinico, DateTime.UtcNow));
    }

    private ConsultaService CriarServico(params ICancelamentoStrategy[] strategies) =>
        new(_repoMock.Object, _pagamentoRepoMock.Object, _documentoRepoMock.Object, _animalRepoMock.Object,
            _consentimentoRepoMock.Object, _responsavelRepoMock.Object, _vetRepoMock.Object,
            _acessoProntuarioServiceMock.Object, _avaliacaoServiceMock.Object, _fidelidadeServiceMock.Object,
            _currentUserMock.Object, _timeProvider, strategies);

    private static CriarConsultaDto CriarDto(TipoServico tipoServico = TipoServico.Consulta, ModalidadeAtendimento modalidade = ModalidadeAtendimento.Presencial) => new()
    {
        DataHora = DateTime.UtcNow.AddDays(1),
        Modalidade = modalidade,
        TipoServico = tipoServico,
        VeterinarioId = Guid.NewGuid(),
        AnimalId = Guid.NewGuid(),
        ResponsavelId = Guid.NewGuid(),
        PreSintomas = "Vomito ha 2 dias"
    };

    private Consulta CriarConsultaConfirmada(Guid veterinarioId = default)
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            veterinarioId == default ? Guid.NewGuid() : veterinarioId, Guid.NewGuid(), Guid.NewGuid());
        consulta.IniciarCheckout(_timeProvider.GetUtcNow().UtcDateTime);
        consulta.ConfirmarPagamento(_timeProvider.GetUtcNow().UtcDateTime);
        return consulta;
    }

    [Fact]
    public async Task AgendarAsync_DadosValidos_CriaConsultaEmCheckoutComLock()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Consulta>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().AgendarAsync(CriarDto());

        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal(StatusConsulta.EmCheckout, resultado.Status);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime.AddMinutes(10), resultado.LockCheckoutExpiraEm);
        Assert.Equal("Vomito ha 2 dias", resultado.PreSintomas);
    }

    [Fact]
    public async Task AgendarAsync_SemConsentimentoClinicoAtivo_LancaBusinessRuleExceptionLGPD001()
    {
        _consentimentoRepoMock
            .Setup(r => r.ObterAtivoAsync(It.IsAny<Guid>(), FinalidadeConsentimento.AtendimentoClinico))
            .ReturnsAsync((ConsentimentoLgpd?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().AgendarAsync(CriarDto()));

        Assert.Equal("LGPD-001", ex.Codigo);
    }

    [Fact]
    public async Task AgendarAsync_ServicoFisicoComModalidadeRemota_LancaBusinessRuleExceptionRN057()
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().AgendarAsync(CriarDto(TipoServico.Cirurgia, ModalidadeAtendimento.Remoto)));

        Assert.Equal("RN-057", ex.Codigo);
    }

    [Fact]
    public async Task AgendarAsync_ServicoFisicoComModalidadePresencial_Sucesso()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Consulta>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().AgendarAsync(CriarDto(TipoServico.Vacinacao, ModalidadeAtendimento.Presencial));

        Assert.Equal(StatusConsulta.EmCheckout, resultado.Status);
    }

    [Fact]
    public async Task ConfirmarPagamentoAsync_DentroDoLock_TransicionaParaConfirmada()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.IniciarCheckout(_timeProvider.GetUtcNow().UtcDateTime);

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().ConfirmarPagamentoAsync(consulta.Id);

        Assert.Equal(StatusConsulta.Confirmada, resultado.Status);
    }

    [Fact]
    public async Task ConfirmarPagamentoAsync_LockExpirado_LancaDomainExceptionCONSULTA011()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.IniciarCheckout(_timeProvider.GetUtcNow().UtcDateTime);
        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);

        _timeProvider.Avancar(TimeSpan.FromMinutes(11));

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => CriarServico().ConfirmarPagamentoAsync(consulta.Id));

        Assert.Equal("CONSULTA-011", ex.Codigo);
    }

    [Fact]
    public async Task CancelarAsync_ConsultaJaCancelada_LancaDomainExceptionCONSULTA010()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddHours(25), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.Cancelar();

        var pagamento = new Pagamento(Guid.NewGuid(), 200m, MeioPagamento.Pix, consulta.Id);
        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _pagamentoRepoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(pagamento);

        var ex = await Assert.ThrowsAsync<DomainException>(
            () => CriarServico().CancelarAsync(consulta.Id));

        Assert.Equal("CONSULTA-010", ex.Codigo);
    }

    [Fact]
    public async Task CancelarAsync_Antecedencia25h_UsaReembolsoIntegralStrategy()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddHours(25), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
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
    public async Task MarcarRealizadaAsync_ComReceitaAssinadaEVetResponsavel_Sucesso()
    {
        var vetId = Guid.NewGuid();
        var consulta = CriarConsultaConfirmada(vetId);
        var receita = new Documento(TipoDocumento.ReceitaVeterinaria, "12345-SP", consulta.Id);
        receita.Assinar();

        _currentUserMock.Setup(c => c.EntidadeId).Returns(vetId);
        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _documentoRepoMock.Setup(r => r.ObterPorConsultaETipoAsync(consulta.Id, TipoDocumento.ReceitaVeterinaria))
            .ReturnsAsync(receita);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().MarcarRealizadaAsync(consulta.Id);

        Assert.Equal(StatusConsulta.Realizada, resultado.Status);
        Assert.NotNull(resultado.DataRealizada);
    }

    [Fact]
    public async Task MarcarRealizadaAsync_ChamadoPorOutroVeterinario_LancaForbiddenExceptionACESSO002()
    {
        var consulta = CriarConsultaConfirmada(Guid.NewGuid());
        _currentUserMock.Setup(c => c.EntidadeId).Returns(Guid.NewGuid()); // vet diferente do responsavel
        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => CriarServico().MarcarRealizadaAsync(consulta.Id));

        Assert.Equal("ACESSO-002", ex.Codigo);
    }

    [Fact]
    public async Task MarcarRealizadaAsync_SemReceita_LancaBusinessRuleExceptionRN031()
    {
        var consulta = CriarConsultaConfirmada();

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _documentoRepoMock.Setup(r => r.ObterPorConsultaETipoAsync(consulta.Id, TipoDocumento.ReceitaVeterinaria))
            .ReturnsAsync((Documento?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().MarcarRealizadaAsync(consulta.Id));

        Assert.Equal("RN-031", ex.Codigo);
    }

    [Fact]
    public async Task MarcarRealizadaAsync_ReceitaNaoAssinada_LancaBusinessRuleExceptionRN031()
    {
        var consulta = CriarConsultaConfirmada();
        var receita = new Documento(TipoDocumento.ReceitaVeterinaria, "12345-SP", consulta.Id);
        // NÃO chama receita.Assinar()

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _documentoRepoMock.Setup(r => r.ObterPorConsultaETipoAsync(consulta.Id, TipoDocumento.ReceitaVeterinaria))
            .ReturnsAsync(receita);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().MarcarRealizadaAsync(consulta.Id));

        Assert.Equal("RN-031", ex.Codigo);
    }

    [Fact]
    public async Task RegistrarNoShowAsync_ParteResponsavel_TransicionaERegistraNoShowNoResponsavel()
    {
        var consulta = CriarConsultaConfirmada();
        var responsavel = new Responsavel("Responsavel Teste", "r@teste.com", "11999999999");
        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(consulta.ResponsavelId)).ReturnsAsync(responsavel);
        _responsavelRepoMock.Setup(r => r.Atualizar(It.IsAny<Responsavel>()));

        var resultado = await CriarServico().RegistrarNoShowAsync(consulta.Id, ParteNoShow.Responsavel);

        Assert.Equal(StatusConsulta.NoShowResponsavel, resultado.Status);
        Assert.Equal(1, responsavel.ContadorNoShows);
    }

    [Fact]
    public async Task RegistrarNoShowAsync_ParteVeterinario_CreditaCortesiaERegistraStrike()
    {
        var vetId = Guid.NewGuid();
        var consulta = CriarConsultaConfirmada(vetId);
        var pagamento = new Pagamento(consulta.ResponsavelId, 300m, MeioPagamento.Pix, consulta.Id);
        var vet = new Veterinario("Dr. Vet", new Crmv("12345-SP"), "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        var responsavel = new Responsavel("Responsavel Teste", "r@teste.com", "11999999999");

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _pagamentoRepoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(pagamento);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(consulta.ResponsavelId)).ReturnsAsync(responsavel);
        _responsavelRepoMock.Setup(r => r.Atualizar(It.IsAny<Responsavel>()));
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vetId)).ReturnsAsync(vet);
        _vetRepoMock.Setup(r => r.Atualizar(It.IsAny<Veterinario>()));

        var resultado = await CriarServico().RegistrarNoShowAsync(consulta.Id, ParteNoShow.Veterinario);

        Assert.Equal(StatusConsulta.NoShowVeterinario, resultado.Status);
        Assert.Equal(30m, responsavel.SaldoCreditosVetly); // 10% de 300 = 30, dentro do teto
        Assert.Single(vet.StrikesAtivos);
    }

    [Fact]
    public async Task CancelamentoPeloVeterinarioAsync_ConsultaR500_CreditaTetoDeR30()
    {
        var vetId = Guid.NewGuid();
        var consulta = CriarConsultaConfirmada(vetId);
        var pagamento = new Pagamento(consulta.ResponsavelId, 500m, MeioPagamento.Pix, consulta.Id);
        var vet = new Veterinario("Dr. Vet", new Crmv("12345-SP"), "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        var responsavel = new Responsavel("Responsavel Teste", "r@teste.com", "11999999999");

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _pagamentoRepoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(pagamento);
        _responsavelRepoMock.Setup(r => r.ObterPorIdAsync(consulta.ResponsavelId)).ReturnsAsync(responsavel);
        _responsavelRepoMock.Setup(r => r.Atualizar(It.IsAny<Responsavel>()));
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vetId)).ReturnsAsync(vet);
        _vetRepoMock.Setup(r => r.Atualizar(It.IsAny<Veterinario>()));

        var resultado = await CriarServico().CancelamentoPeloVeterinarioAsync(consulta.Id);

        Assert.Equal(30m, resultado.CreditoCortesia); // 10% de 500 = 50, mas teto e R$30
        Assert.True(resultado.StrikeRegistrado);
        Assert.False(resultado.VeterinarioSuspenso); // 1o strike, nao atinge o limiar de 3
        Assert.Equal(StatusConsulta.Cancelada, consulta.Status);
    }

    [Fact]
    public async Task CancelamentoPeloVeterinarioAsync_TerceiroStrikeEm90Dias_SuspendeVeterinarioPor7Dias()
    {
        var vetId = Guid.NewGuid();
        var vet = new Veterinario("Dr. Vet", new Crmv("12345-SP"), "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        vet.RegistrarStrike(_timeProvider.GetUtcNow().UtcDateTime, "strike anterior 1");
        vet.RegistrarStrike(_timeProvider.GetUtcNow().UtcDateTime, "strike anterior 2");

        var consulta = CriarConsultaConfirmada(vetId);
        var responsavel = new Responsavel("Responsavel Teste", "r@teste.com", "11999999999");

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _pagamentoRepoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync((Pagamento?)null);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vetId)).ReturnsAsync(vet);
        _vetRepoMock.Setup(r => r.Atualizar(It.IsAny<Veterinario>()));

        var resultado = await CriarServico().CancelamentoPeloVeterinarioAsync(consulta.Id);

        Assert.Equal(0m, resultado.CreditoCortesia); // sem pagamento vinculado, nada a creditar
        Assert.True(resultado.VeterinarioSuspenso);
        Assert.NotNull(vet.SuspensoAte);
    }

    [Fact]
    public async Task RemarcarAsync_ConsultaConfirmada_IncrementaContadorRemarcacoes()
    {
        var consulta = CriarConsultaConfirmada();
        var novaData = DateTime.UtcNow.AddDays(3);
        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().RemarcarAsync(consulta.Id, novaData);

        Assert.Equal(novaData, resultado.DataHora);
        Assert.Equal(1, resultado.ContadorRemarcacoes);
    }

    [Fact]
    public async Task ValidarDiagnosticoAsync_ConsultaExistente_MarcaDiagnosticoValidado()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
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
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.Cancelar();

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ValidarDiagnosticoAsync(consulta.Id));

        Assert.Equal("CONSULTA-003", ex.Codigo);
    }
}
