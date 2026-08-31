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
    private readonly Mock<IAgendaRepository> _agendaRepoMock = new();
    private readonly Mock<IFilaDeJobs> _filaMock = new();

    private ConsultaService CriarServico(params ICancelamentoStrategy[] strategies) =>
        new(_repoMock.Object, _pagamentoRepoMock.Object, _documentoRepoMock.Object,
            _animalRepoMock.Object, _vetRepoMock.Object, _empresaRepoMock.Object,
            strategies, _usuarioMock.Object, _agendaRepoMock.Object, _filaMock.Object);

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

    // ── Checkout com lock (RN-035, C-02) ─────────────────────────────────────

    /// <summary>Monta o cenario feliz do checkout e devolve os ids envolvidos.</summary>
    private (Animal Animal, Veterinario Vet, Slot Slot, Servico Servico) PrepararCheckout(
        PersonaVeterinario persona = PersonaVeterinario.Autonomo, Guid? empresaId = null)
    {
        var animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());

        var vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP", persona, PlanoAssinatura.Profissional);
        if (empresaId is { } id) vet.VincularEmpresa(id);

        var slot = new Slot(vet.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddMinutes(30));
        var prestadorId = empresaId ?? vet.Id;
        var servico = new Servico(prestadorId, TipoServico.ConsultaRotina, 200m, 30);

        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _agendaRepoMock.Setup(r => r.ObterSlotAsync(slot.Id)).ReturnsAsync(slot);
        _agendaRepoMock.Setup(r => r.ObterServicoAsync(servico.Id)).ReturnsAsync(servico);
        _agendaRepoMock.Setup(r => r.AtualizarSlot(It.IsAny<Slot>()));
        _agendaRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Consulta>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        return (animal, vet, slot, servico);
    }

    private static CheckoutDto CheckoutDe(Animal animal, Guid prestadorId, Slot slot, Servico servico) => new()
    {
        AnimalId = animal.Id,
        PrestadorId = prestadorId,
        SlotId = slot.Id,
        ServicoId = servico.Id
    };

    [Fact]
    public async Task Checkout_TravaOHorarioECriaAConsultaEmCheckout()
    {
        var (animal, vet, slot, servico) = PrepararCheckout();

        var resultado = await CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, slot, servico));

        Assert.Equal(StatusConsulta.EmCheckout, resultado.Status);
        Assert.Equal(EstadoSlot.EmCheckout, slot.Estado);
        Assert.Equal(slot.Inicio, resultado.Resumo.DataHora);
        Assert.Equal(200m, resultado.Resumo.Valor);
    }

    [Fact]
    public async Task Checkout_DevolveOInstanteEmQueOLockExpira()
    {
        var (animal, vet, slot, servico) = PrepararCheckout();

        var antes = DateTime.UtcNow;
        var resultado = await CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, slot, servico));

        // O Responsavel precisa saber quanto tempo tem para pagar (RN-035)
        Assert.InRange(resultado.LockExpiraEm, antes.AddMinutes(9), antes.AddMinutes(11));
    }

    [Fact]
    public async Task Checkout_ExibeAPoliticaDeReembolsoAntesDeCobrar()
    {
        var (animal, vet, slot, servico) = PrepararCheckout();

        var resultado = await CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, slot, servico));
        var politica = resultado.Resumo.PoliticaReembolso;

        // Transparencia no momento do agendamento, nao depois (RN-042)
        Assert.Equal(24, politica.IntegralAcimaDeHoras);
        Assert.Equal(2, politica.SemReembolsoAbaixoDeHoras);
        Assert.Equal(30m, politica.PercentualRetencaoParcial);
    }

    [Fact]
    public async Task Checkout_HorarioJaReservado_Retorna409()
    {
        var (animal, vet, slot, servico) = PrepararCheckout();
        slot.TravarParaCheckout(Guid.NewGuid(), DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, slot, servico)));

        // 409 e nao 422: tentar de novo com outro horario resolve
        Assert.Equal("RN-035", ex.Codigo);
    }

    [Fact]
    public async Task Checkout_HorarioNoPassado_NaoEAceito()
    {
        var (animal, vet, _, servico) = PrepararCheckout();
        var passado = new Slot(vet.Id, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(-1).AddMinutes(30));
        _agendaRepoMock.Setup(r => r.ObterSlotAsync(passado.Id)).ReturnsAsync(passado);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, passado, servico)));

        Assert.Equal("RN-034", ex.Codigo);
    }

    [Fact]
    public async Task Checkout_ServicoDeOutroPrestador_NaoEAceito()
    {
        var (animal, vet, slot, _) = PrepararCheckout();
        var deOutro = new Servico(Guid.NewGuid(), TipoServico.ConsultaRotina, 200m, 30);
        _agendaRepoMock.Setup(r => r.ObterServicoAsync(deOutro.Id)).ReturnsAsync(deOutro);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, slot, deOutro)));

        Assert.Equal("RN-032", ex.Codigo);
    }

    [Fact]
    public async Task Checkout_ServicoDesativado_NaoEAceito()
    {
        var (animal, vet, slot, servico) = PrepararCheckout();
        servico.Desativar();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, slot, servico)));

        Assert.Equal("RN-032", ex.Codigo);
    }

    [Fact]
    public async Task Checkout_ComClinica_AtribuiAoDonoDoHorario()
    {
        var empresaId = Guid.NewGuid();
        var (animal, vet, slot, servico) = PrepararCheckout(PersonaVeterinario.Vinculado, empresaId);

        var empresa = new Empresa("Clinica Vida Pet", "Clinica", Guid.NewGuid());
        _empresaRepoMock.Setup(r => r.ObterPorIdAsync(empresaId)).ReturnsAsync(empresa);

        Consulta? criada = null;
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Consulta>()))
            .Callback<Consulta>(c => criada = c).Returns(Task.CompletedTask);

        var resultado = await CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, empresaId, slot, servico));

        // RN-003: com clinica, quem atende e o profissional dono do horario escolhido
        Assert.NotNull(criada);
        Assert.Equal(vet.Id, criada!.VeterinarioId);
        Assert.Equal(empresaId, criada.EmpresaId);
        Assert.Equal("Clinica Vida Pet", resultado.Resumo.Prestador);
    }

    [Fact]
    public async Task Checkout_HorarioDeVetForaDaClinicaInformada_NaoEAceito()
    {
        var (animal, _, slot, servico) = PrepararCheckout();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, Guid.NewGuid(), slot, servico)));

        Assert.Equal("RN-032", ex.Codigo);
    }

    [Fact]
    public async Task Checkout_ComAnimalDeOutroResponsavel_ERecusado()
    {
        var (animal, vet, slot, servico) = PrepararCheckout();
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhTutor).Returns(true);
        _usuarioMock.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, slot, servico)));

        Assert.Equal("RN-105", ex.Codigo);
    }

    [Fact]
    public async Task Checkout_MarcaAOrigemComoCheckout()
    {
        var (animal, vet, slot, servico) = PrepararCheckout();

        Consulta? criada = null;
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Consulta>()))
            .Callback<Consulta>(c => criada = c).Returns(Task.CompletedTask);

        await CriarServico().IniciarCheckoutAsync(CheckoutDe(animal, vet.Id, slot, servico));

        // Distingue do POST /api/consultas, que e emergencia/balcao (RN-040)
        Assert.Equal(OrigemConsulta.Checkout, criada!.Origem);
        Assert.Equal(slot.Id, criada.SlotId);
        Assert.Equal(servico.Id, criada.ServicoId);
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

    // ── Finalizacao e assinatura (RN-087, C-04) ──────────────────────────────

    private Consulta ConsultaComDocumentos(params Documento[] documentos)
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        _documentoRepoMock.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(documentos);

        return consulta;
    }

    [Fact]
    public async Task FinalizarAsync_ComReceitaAssinada_Sucesso()
    {
        var receita = new Documento(TipoDocumento.ReceitaVeterinaria, "12345-SP", Guid.NewGuid());
        receita.Assinar();

        var consulta = ConsultaComDocumentos(receita);

        await CriarServico().FinalizarAsync(consulta.Id);

        Assert.True(consulta.Finalizada);
    }

    [Fact]
    public async Task FinalizarAsync_SemNenhumDocumentoQueExigeAssinatura_Sucesso()
    {
        // C-04: consulta de rotina, vacinacao ou retorno frequentemente nao prescrevem
        // nada. Exigir receita em todas levaria o veterinario a emitir receita vazia so
        // para conseguir fechar a consulta — o oposto do que a RN-087 protege.
        var consulta = ConsultaComDocumentos(
            new Documento(TipoDocumento.Prontuario, "12345-SP", Guid.NewGuid()));

        await CriarServico().FinalizarAsync(consulta.Id);

        Assert.True(consulta.Finalizada);
    }

    [Fact]
    public async Task FinalizarAsync_SemDocumentoAlgum_Sucesso()
    {
        var consulta = ConsultaComDocumentos();

        await CriarServico().FinalizarAsync(consulta.Id);

        Assert.True(consulta.Finalizada);
    }

    [Fact]
    public async Task FinalizarAsync_ReceitaNaoAssinada_LancaBusinessRuleExceptionRN087()
    {
        var receita = new Documento(TipoDocumento.ReceitaVeterinaria, "12345-SP", Guid.NewGuid());
        // NÃO chama receita.Assinar()

        var consulta = ConsultaComDocumentos(receita);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().FinalizarAsync(consulta.Id));

        Assert.Equal("RN-087", ex.Codigo);
    }

    [Fact]
    public async Task FinalizarAsync_AtestadoNaoAssinado_LancaBusinessRuleExceptionRN087()
    {
        // O atestado tambem sai da plataforma afirmando algo em nome do profissional
        var atestado = new Documento(TipoDocumento.Atestado, "12345-SP", Guid.NewGuid());

        var consulta = ConsultaComDocumentos(atestado);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().FinalizarAsync(consulta.Id));

        Assert.Equal("RN-087", ex.Codigo);
    }

    [Fact]
    public async Task FinalizarAsync_ProntuarioNaoAssinado_NaoBloqueia()
    {
        var prontuario = new Documento(TipoDocumento.Prontuario, "12345-SP", Guid.NewGuid());
        var recibo = new Documento(TipoDocumento.NotaFiscal, "12345-SP", Guid.NewGuid());

        var consulta = ConsultaComDocumentos(prontuario, recibo);

        await CriarServico().FinalizarAsync(consulta.Id);

        Assert.True(consulta.Finalizada);
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

    // As duas verificacoes de validacao de diagnostico que ficavam aqui foram para
    // ProntuarioServiceTests: RN-082 deixou de ser um booleano ligado por
    // ConsultaService e passou a ser a decisao em tres caminhos, registrada na trilha
    // de auditoria. O comportamento continua coberto — diagnostico validado ao
    // aprovar, e CONSULTA-003 em consulta cancelada.
}
