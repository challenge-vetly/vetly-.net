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
/// Pre-sintomas, simulacao de cancelamento, remarcacao e no-show
/// (RN-005/RN-013/RN-036/RN-041/RN-043/RN-044).
/// </summary>
public class AgendamentoTests
{
    private readonly Mock<IConsultaRepository> _repo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IDocumentoRepository> _documentoRepo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IEmpresaRepository> _empresaRepo = new();
    private readonly Mock<IAgendaRepository> _agendaRepo = new();
    private readonly Mock<IFilaDeJobs> _fila = new();
    private readonly Mock<IFidelidadeService> _fidelidade = new();
    private readonly Mock<IAvaliacaoService> _avaliacoes = new();
    private readonly Mock<IColmeiaService> _colmeia = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly Veterinario _vet;
    private readonly Slot _slotOriginal;
    private readonly Consulta _consulta;

    public AgendamentoTests()
    {
        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _slotOriginal = new Slot(
            _vet.Id, DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(3).AddMinutes(30));

        _consulta = Consulta.ParaCheckout(
            _slotOriginal.Inicio, _vet.Id, Guid.NewGuid(), _tutorId, _slotOriginal.Id, Guid.NewGuid());

        _consulta.ConfirmarPagamento();

        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);

        _repo.Setup(r => r.ObterPorIdAsync(_consulta.Id)).ReturnsAsync(_consulta);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);
        _agendaRepo.Setup(r => r.ObterSlotAsync(_slotOriginal.Id)).ReturnsAsync(_slotOriginal);
        _agendaRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
    }

    private readonly Mock<INotificacaoService> _notificacoes = new();

    private ConsultaService CriarServico() =>
        new(_repo.Object, _pagamentoRepo.Object, _documentoRepo.Object, _animalRepo.Object,
            _vetRepo.Object, _empresaRepo.Object,
            [new ReembolsoIntegralStrategy(), new ReembolsoParcialStrategy(), new SemReembolsoStrategy()],
            _usuario.Object, _agendaRepo.Object, _fila.Object, _fidelidade.Object, _avaliacoes.Object, _colmeia.Object,
            _notificacoes.Object);

    private Pagamento Pagamento(decimal valor = 200m)
    {
        var pagamento = new Pagamento(_tutorId, valor, MeioPagamento.Pix, _consulta.Id);
        pagamento.Confirmar();

        _pagamentoRepo.Setup(r => r.ObterPorConsultaAsync(_consulta.Id)).ReturnsAsync(pagamento);

        return pagamento;
    }

    private Slot NovoSlot(int emDias = 5, Guid? vetId = null)
    {
        var inicio = DateTime.UtcNow.AddDays(emDias);
        var slot = new Slot(vetId ?? _vet.Id, inicio, inicio.AddMinutes(30));

        _agendaRepo.Setup(r => r.ObterSlotAsync(slot.Id)).ReturnsAsync(slot);

        return slot;
    }

    private static PreSintomasDto PreSintomas() => new()
    {
        QueixaPrincipal = "Vomito desde ontem",
        DuracaoEmDias = 1,
        SinaisObservados = ["Vomito", "Apatia"],
        AlimentacaoNormal = false,
        MidiaIds = [Guid.NewGuid()]
    };

    // ── Pré-sintomas (RN-005/RN-036) ─────────────────────────────────────────

    [Fact]
    public async Task PreSintomas_AntesDoAtendimento_SaoGravados()
    {
        await CriarServico().RegistrarPreSintomasAsync(_consulta.Id, PreSintomas());

        Assert.Contains("Vomito desde ontem", _consulta.PreSintomas);
        Assert.NotNull(_consulta.PreSintomasMidias);
    }

    [Fact]
    public async Task PreSintomas_DepoisDoAtendimento_NaoSaoAceitos()
    {
        _consulta.Finalizar();

        // O briefing ja foi lido e a IA ja recebeu o contexto: informar ali nao
        // alimenta nada (RN-005/RN-078)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().RegistrarPreSintomasAsync(_consulta.Id, PreSintomas()));

        Assert.Equal("RN-036", ex.Codigo);
    }

    [Fact]
    public async Task PreSintomas_DeConsultaAlheia_SaoRecusados()
    {
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarPreSintomasAsync(_consulta.Id, PreSintomas()));
    }

    [Fact]
    public async Task PreSintomas_SemMidia_GuardamOSentinela()
    {
        var semMidia = PreSintomas();
        semMidia.MidiaIds = [];

        await CriarServico().RegistrarPreSintomasAsync(_consulta.Id, semMidia);

        // No Oracle a string vazia E NULL: distinguir "sem midia" de "nao informado"
        // importa no briefing
        Assert.Equal(";", _consulta.PreSintomasMidias);
    }

    // ── Simulação de cancelamento (RN-014/RN-041/RN-042) ─────────────────────

    [Fact]
    public async Task Simulacao_MostraOValorSemExecutarOCancelamento()
    {
        var pagamento = Pagamento();

        var simulacao = await CriarServico().SimularCancelamentoAsync(_consulta.Id);

        // Mais de 24h de antecedencia: reembolso integral (RN-041)
        Assert.Equal(200m, simulacao.ValorReembolso);
        Assert.Equal(0m, simulacao.ValorRetido);
        Assert.Equal("Simulada", simulacao.Liquidacao);

        // Descobrir a retencao depois de cancelar e descobrir tarde demais
        Assert.False(_consulta.Cancelada);
        Assert.Equal(StatusPagamento.Confirmado, pagamento.StatusPagamento);
    }

    [Fact]
    public async Task Simulacao_NaFaixaParcial_MostraARetencaoDaClinica()
    {
        _consulta.Reagendar(DateTime.UtcNow.AddHours(20));
        Pagamento();

        var simulacao = await CriarServico().SimularCancelamentoAsync(_consulta.Id);

        // 30% e o default do seed quando a clinica nao configurou (RN-042)
        Assert.Equal(30m, simulacao.PercentualRetencao);
        Assert.Equal(60m, simulacao.ValorRetido);
        Assert.Equal(140m, simulacao.ValorReembolso);
    }

    [Fact]
    public async Task Simulacao_ConsultaJaCancelada_NaoFazSentido()
    {
        Pagamento();
        _consulta.Cancelar();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().SimularCancelamentoAsync(_consulta.Id));

        Assert.Equal("CONSULTA-001", ex.Codigo);
    }

    // ── Remarcação (RN-013/RN-043) ───────────────────────────────────────────

    [Fact]
    public async Task Remarcacao_TransfereOHorarioEOPagamento()
    {
        var novo = NovoSlot();

        var resultado = await CriarServico().RemarcarAsync(
            _consulta.Id, new RemarcarConsultaDto { NovoSlotId = novo.Id });

        Assert.Equal(novo.Inicio, _consulta.DataHora);

        // RN-013: o pagamento acompanha a nova data, sem nova cobranca
        Assert.Equal(StatusPagamento.Confirmado, resultado.StatusPagamento);
        Assert.Equal(1, resultado.Remarcacoes);
        Assert.Equal(1, resultado.RemarcacoesRestantes);
    }

    [Fact]
    public async Task Remarcacao_TravaONovoHorarioELiberaOAntigo()
    {
        var novo = NovoSlot();

        await CriarServico().RemarcarAsync(_consulta.Id, new RemarcarConsultaDto { NovoSlotId = novo.Id });

        Assert.Equal(EstadoSlot.Confirmado, novo.Estado);
        Assert.Equal(EstadoSlot.Livre, _slotOriginal.Estado);

        // O horario antigo e vaga que alguem esta esperando (RN-037)
        _fila.Verify(f => f.EnfileirarAsync(
            TipoJob.PromoverListaEspera, _slotOriginal.Id.ToString(), null), Times.Once);
    }

    [Fact]
    public async Task Remarcacao_TerceiraVez_NaoEPermitida()
    {
        var servico = CriarServico();

        await servico.RemarcarAsync(_consulta.Id, new RemarcarConsultaDto { NovoSlotId = NovoSlot(5).Id });
        await servico.RemarcarAsync(_consulta.Id, new RemarcarConsultaDto { NovoSlotId = NovoSlot(7).Id });

        // RN-043: acima de duas, remarcar vira burla a janela de reembolso — quem quer
        // desistir sem perder dinheiro empurraria a data indefinidamente
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => servico.RemarcarAsync(_consulta.Id, new RemarcarConsultaDto { NovoSlotId = NovoSlot(9).Id }));

        Assert.Equal("RN-043", ex.Codigo);
        Assert.Equal(2, _consulta.ContadorRemarcacoes);
    }

    [Fact]
    public async Task Remarcacao_ParaHorarioDeOutroVeterinario_NaoEAceita()
    {
        var deOutro = NovoSlot(vetId: Guid.NewGuid());

        // Trocar de profissional e cancelar e agendar de novo, ou redistribuir
        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().RemarcarAsync(_consulta.Id, new RemarcarConsultaDto { NovoSlotId = deOutro.Id }));
    }

    [Fact]
    public async Task Remarcacao_ParaHorarioJaOcupado_Retorna409()
    {
        var novo = NovoSlot();
        novo.TravarParaCheckout(Guid.NewGuid(), DateTime.UtcNow);

        await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().RemarcarAsync(_consulta.Id, new RemarcarConsultaDto { NovoSlotId = novo.Id }));
    }

    [Fact]
    public void Remarcacao_DeConsultaRealizada_NaoFazSentido()
    {
        _consulta.Finalizar();

        Assert.Throws<InvalidOperationException>(
            () => _consulta.RemarcarPara(DateTime.UtcNow.AddDays(5), Guid.NewGuid()));
    }

    // ── No-show (RN-038/RN-044) ──────────────────────────────────────────────

    [Fact]
    public async Task NoShow_RegistradoPeloVeterinario_NaoGeraReembolso()
    {
        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);

        var resultado = await CriarServico().RegistrarNoShowAsync(_consulta.Id);

        // RN-044: segue a faixa "menos de 2h ou no ato" da RN-014 — nao ha penalidade
        // nova, so a politica que ja existia
        Assert.Equal(StatusConsulta.NoShow, resultado.Status);
        Assert.False(resultado.GerouReembolso);
    }

    [Fact]
    public async Task NoShow_DeclaradoPeloProprioResponsavel_ERecusado()
    {
        // Quem registra e quem estava esperando
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarNoShowAsync(_consulta.Id));

        Assert.Equal("RN-105", ex.Codigo);
    }

    [Fact]
    public async Task NoShow_DeConsultaNaoConfirmada_NaoEPermitido()
    {
        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);
        _consulta.Finalizar();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().RegistrarNoShowAsync(_consulta.Id));

        Assert.Equal("RN-044", ex.Codigo);
    }
}
