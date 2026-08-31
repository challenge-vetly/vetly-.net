using Moq;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Application.Strategies.Split;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Cobranca e webhook de pagamento (RN-006/RN-035/RN-070, vetly-tech §7.5).
///
/// O ponto central: o estado autoritativo vem do webhook, nunca da resposta sincrona
/// da criacao da cobranca.
/// </summary>
public class PagamentoWebhookTests
{
    private readonly Mock<IPagamentoRepository> _repo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IEmpresaRepository> _empresaRepo = new();
    private readonly Mock<IPagamentoAdapter> _adaptador = new();
    private readonly Mock<IAgendaRepository> _agendaRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private const string Referencia = "sim_referencia-de-teste";

    public PagamentoWebhookTests()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.Atualizar(It.IsAny<Pagamento>()));
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _consultaRepo.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _consultaRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _agendaRepo.Setup(r => r.AtualizarSlot(It.IsAny<Slot>()));
        _agendaRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        _adaptador
            .Setup(a => a.CriarCobrancaAsync(It.IsAny<CriarCobrancaRequest>()))
            .ReturnsAsync(new CobrancaCriadaDto(Referencia, "PixSimulado|vetly-sim-abc123", StatusPagamento.Pendente));
    }

    private PagamentoService CriarServico() =>
        new(_repo.Object, _vetRepo.Object, _consultaRepo.Object, _empresaRepo.Object,
            _adaptador.Object, _agendaRepo.Object,
            [new SplitBasicoStrategy(), new SplitProfissionalStrategy(), new SplitEnterpriseStrategy()],
            _usuario.Object);

    /// <summary>Monta consulta em checkout, com horario travado e pagamento pendente.</summary>
    private (Pagamento Pagamento, Consulta Consulta, Slot Slot) CenarioEmCheckout()
    {
        var vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        var slot = new Slot(vet.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddMinutes(30));

        var consulta = Consulta.ParaCheckout(
            slot.Inicio, vet.Id, Guid.NewGuid(), Guid.NewGuid(), slot.Id, Guid.NewGuid());

        slot.TravarParaCheckout(consulta.Id, DateTime.UtcNow);

        var pagamento = new Pagamento(Guid.NewGuid(), 200m, MeioPagamento.Pix, consulta.Id);
        pagamento.RegistrarCobranca(Referencia, "chave");

        _repo.Setup(r => r.ObterPorReferenciaExternaAsync(Referencia)).ReturnsAsync(pagamento);
        _repo.Setup(r => r.ObterPorIdAsync(pagamento.Id)).ReturnsAsync(pagamento);
        _consultaRepo.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _vetRepo.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _agendaRepo.Setup(r => r.ObterSlotAsync(slot.Id)).ReturnsAsync(slot);

        return (pagamento, consulta, slot);
    }

    private void EventoDoProvedor(StatusPagamento status, bool assinado = true) =>
        _adaptador
            .Setup(a => a.ReceberWebhookDeStatusAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync(new WebhookStatusDto(Referencia, status, assinado));

    // ── Criação da cobrança (RN-006/RN-070) ──────────────────────────────────

    [Fact]
    public async Task CriarCobranca_NaoConfirmaOPagamento()
    {
        Pagamento? criado = null;
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<Pagamento>()))
            .Callback<Pagamento>(p => criado = p).Returns(Task.CompletedTask);

        var resposta = await CriarServico().CriarCobrancaAsync(new CriarPagamentoDto
        {
            TutorId = Guid.NewGuid(), Valor = 200m, MeioPagamento = MeioPagamento.Pix
        });

        // Quem confirma e o webhook, nunca a resposta sincrona (vetly-tech §7.5)
        Assert.Equal(StatusPagamento.Pendente, resposta.StatusPagamento);
        Assert.Equal(StatusPagamento.Pendente, criado!.StatusPagamento);
    }

    [Fact]
    public async Task CriarCobranca_GuardaAReferenciaDoProvedorEDevolveAsInstrucoes()
    {
        var resposta = await CriarServico().CriarCobrancaAsync(new CriarPagamentoDto
        {
            TutorId = Guid.NewGuid(), Valor = 200m, MeioPagamento = MeioPagamento.Pix
        });

        Assert.Equal(Referencia, resposta.Instrucoes.ReferenciaExterna);
        Assert.Equal("PixSimulado", resposta.Instrucoes.Tipo);
        Assert.Equal("vetly-sim-abc123", resposta.Instrucoes.Codigo);
        Assert.Equal("Simulada", resposta.Liquidacao);
    }

    [Fact]
    public async Task CriarCobranca_ComConsulta_JaApuraOSplit()
    {
        var (_, consulta, _) = CenarioEmCheckout();

        var resposta = await CriarServico().CriarCobrancaAsync(new CriarPagamentoDto
        {
            TutorId = Guid.NewGuid(), Valor = 200m, MeioPagamento = MeioPagamento.Pix, ConsultaId = consulta.Id
        });

        // O split e calculado pela Vetly, nunca pelo provedor (RN-051/RN-070)
        Assert.Equal(PlanoAssinatura.Profissional, resposta.Split.Plano);
        Assert.Equal(24m, resposta.Split.ComissaoVetly);
        Assert.Equal(176m, resposta.Split.Repasse);
    }

    // ── Webhook: confirmação (RN-006/RN-035) ─────────────────────────────────

    [Fact]
    public async Task Webhook_Confirmado_PromoveAConsultaEOcupaOHorario()
    {
        var (pagamento, consulta, slot) = CenarioEmCheckout();
        EventoDoProvedor(StatusPagamento.Confirmado);

        var resultado = await CriarServico().ProcessarWebhookAsync("{}", "token");

        Assert.Equal(StatusPagamento.Confirmado, pagamento.StatusPagamento);
        Assert.Equal(StatusConsulta.Confirmada, consulta.Status);
        Assert.Equal(EstadoSlot.Confirmado, slot.Estado);
        Assert.False(resultado.Ignorado);
    }

    // ── Webhook: recusa (RN-006/RN-035) ──────────────────────────────────────

    [Fact]
    public async Task Webhook_Recusado_ExpiraAConsultaELiberaOHorario()
    {
        var (pagamento, consulta, slot) = CenarioEmCheckout();
        EventoDoProvedor(StatusPagamento.Recusado);

        await CriarServico().ProcessarWebhookAsync("{}", "token");

        Assert.Equal(StatusPagamento.Recusado, pagamento.StatusPagamento);
        Assert.Equal(StatusConsulta.Expirada, consulta.Status);
        // Segurar o horario de quem nao pagou tiraria a vaga de quem pagaria (RN-035)
        Assert.Equal(EstadoSlot.Livre, slot.Estado);
        Assert.Null(slot.LockConsultaId);
    }

    // ── Webhook: reentrega e segurança ───────────────────────────────────────

    [Fact]
    public async Task Webhook_ReentregueDepoisDeConfirmado_NaoMudaNada()
    {
        var (pagamento, consulta, slot) = CenarioEmCheckout();
        EventoDoProvedor(StatusPagamento.Confirmado);

        await CriarServico().ProcessarWebhookAsync("{}", "token");

        // Agora chega a reentrega, desta vez dizendo recusado
        EventoDoProvedor(StatusPagamento.Recusado);
        var resultado = await CriarServico().ProcessarWebhookAsync("{}", "token");

        // Webhook e entregue mais de uma vez por natureza: reprocessar nao pode
        // reabrir consulta ja confirmada nem soltar o horario
        Assert.True(resultado.Ignorado);
        Assert.Equal(StatusPagamento.Confirmado, pagamento.StatusPagamento);
        Assert.Equal(StatusConsulta.Confirmada, consulta.Status);
        Assert.Equal(EstadoSlot.Confirmado, slot.Estado);
    }

    [Fact]
    public async Task Webhook_NaoAssinado_ERecusado()
    {
        CenarioEmCheckout();
        EventoDoProvedor(StatusPagamento.Confirmado, assinado: false);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ProcessarWebhookAsync("{}", null));

        Assert.Equal("PAGAMENTO-002", ex.Codigo);
    }

    [Fact]
    public async Task Webhook_DeReferenciaDesconhecida_Retorna404()
    {
        EventoDoProvedor(StatusPagamento.Confirmado);
        _repo.Setup(r => r.ObterPorReferenciaExternaAsync(It.IsAny<string>())).ReturnsAsync((Pagamento?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CriarServico().ProcessarWebhookAsync("{}", "token"));
    }

    [Fact]
    public async Task Webhook_ComStatusIntermediario_NaoMexeNaConsulta()
    {
        var (_, consulta, slot) = CenarioEmCheckout();
        EventoDoProvedor(StatusPagamento.Pendente);

        var resultado = await CriarServico().ProcessarWebhookAsync("{}", "token");

        Assert.True(resultado.Ignorado);
        Assert.Equal(StatusConsulta.EmCheckout, consulta.Status);
        Assert.Equal(EstadoSlot.EmCheckout, slot.Estado);
    }

    // ── Polling do app (RN-006) ──────────────────────────────────────────────

    [Fact]
    public async Task Status_EnquantoPendente_IndicaQueAindaAguarda()
    {
        var (pagamento, _, _) = CenarioEmCheckout();

        var status = await CriarServico().ObterStatusAsync(pagamento.Id);

        Assert.True(status.AguardandoConfirmacao);
        Assert.Equal(StatusConsulta.EmCheckout, status.StatusConsulta);
    }

    [Fact]
    public async Task Status_DepoisDeConfirmado_ParaDeAguardar()
    {
        var (pagamento, _, _) = CenarioEmCheckout();
        EventoDoProvedor(StatusPagamento.Confirmado);
        await CriarServico().ProcessarWebhookAsync("{}", "token");

        var status = await CriarServico().ObterStatusAsync(pagamento.Id);

        Assert.False(status.AguardandoConfirmacao);
        Assert.Equal(StatusConsulta.Confirmada, status.StatusConsulta);
    }

    [Fact]
    public async Task Status_DePagamentoDeOutroResponsavel_ERecusado()
    {
        var (pagamento, _, _) = CenarioEmCheckout();
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterStatusAsync(pagamento.Id));

        Assert.Equal("RN-106", ex.Codigo);
    }
}
