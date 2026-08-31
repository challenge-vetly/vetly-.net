using Moq;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.DTOs.Redistribuicao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Redistribuicao de consultas quando o profissional sai ou fica indisponivel
/// (RN-025).
///
/// Cancelar em massa jogaria o problema no colo do Responsavel, que agendou de boa-fe
/// e teria de refazer tudo — inclusive pagar de novo.
/// </summary>
public class RedistribuicaoTests
{
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IAgendaRepository> _agendaRepo = new();
    private readonly Mock<INotificacaoService> _notificacoes = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Veterinario _quemSai;
    private readonly Animal _animal;
    private readonly Consulta _consulta;
    private readonly Slot _slotOriginal;

    public RedistribuicaoTests()
    {
        _quemSai = CriarVet("Dra. Marina", "12345-SP");

        _animal = new Animal("Thor", "Canino", "SRD", new DateTime(2022, 3, 1), Guid.NewGuid());

        _slotOriginal = new Slot(
            _quemSai.Id, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddMinutes(30));

        _consulta = Consulta.ParaCheckout(
            _slotOriginal.Inicio, _quemSai.Id, _animal.Id, _animal.TutorId,
            _slotOriginal.Id, Guid.NewGuid());

        _consulta.ConfirmarPagamento();

        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        _consultaRepo.Setup(r => r.ObterPorIdAsync(_consulta.Id)).ReturnsAsync(_consulta);
        _consultaRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(_animal);
        _vetRepo.Setup(r => r.ObterPorIdAsync(_quemSai.Id)).ReturnsAsync(_quemSai);
        _vetRepo.Setup(r => r.ObterPorUfAsync(It.IsAny<string>())).ReturnsAsync([]);
        _agendaRepo.Setup(r => r.ObterSlotAsync(_slotOriginal.Id)).ReturnsAsync(_slotOriginal);
        _agendaRepo.Setup(r => r.ObterSlotsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _agendaRepo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        _notificacoes.Setup(n => n.CriarAsync(It.IsAny<CriarNotificacaoDto>()))
            .ReturnsAsync(new NotificacaoDto());
    }

    private RedistribuicaoService CriarServico() =>
        new(_consultaRepo.Object, _vetRepo.Object, _animalRepo.Object,
            _agendaRepo.Object, _notificacoes.Object, _usuario.Object);

    private static Veterinario CriarVet(string nome, string crmv, params string[] especies)
    {
        var vet = new Veterinario(nome, new Crmv(crmv), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        foreach (var especie in especies)
            vet.AdicionarEspecie(especie);

        return vet;
    }

    /// <summary>Um candidato publicado, com horário livre a certa distância do original.</summary>
    private (Veterinario Vet, Slot Slot) Candidato(
        string nome, string crmv, double horasDeDiferenca, params string[] especies)
    {
        var vet = CriarVet(nome, crmv, especies);
        vet.RegistrarValidacaoCrmv(StatusCrmv.Valido, DateTime.UtcNow);
        vet.PublicarNoMatching(DateTime.UtcNow);

        var inicio = _consulta.DataHora.AddHours(horasDeDiferenca);
        var slot = new Slot(vet.Id, inicio, inicio.AddMinutes(30));

        _vetRepo.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _agendaRepo.Setup(r => r.ObterSlotAsync(slot.Id)).ReturnsAsync(slot);
        _agendaRepo.Setup(r => r.ObterSlotsAsync(vet.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([slot]);

        return (vet, slot);
    }

    private void NaRegiao(params Veterinario[] vets) =>
        _vetRepo.Setup(r => r.ObterPorUfAsync("SP")).ReturnsAsync([_quemSai, .. vets]);

    private static RedistribuirConsultaDto Pedido(Guid vetId, Guid slotId) => new()
    {
        NovoVeterinarioId = vetId,
        NovoSlotId = slotId,
        Motivo = "O profissional encerrou o cadastro na plataforma."
    };

    // ── Candidatos (RN-025/RN-029) ───────────────────────────────────────────

    [Fact]
    public async Task Candidatos_OrdenaPelaProximidadeDoHorarioOriginal()
    {
        var (longe, _) = Candidato("Dr. Longe", "22222-SP", 20, "Canino");
        var (perto, _) = Candidato("Dra. Perto", "33333-SP", 2, "Canino");

        NaRegiao(longe, perto);

        var candidatos = (await CriarServico().SugerirCandidatosAsync(_consulta.Id)).ToList();

        // Quem agendou as 14h de terca organizou o dia em torno disso
        Assert.Equal(2, candidatos.Count);
        Assert.Equal(perto.Id, candidatos[0].VeterinarioId);
    }

    [Fact]
    public async Task Candidatos_QuemNaoAtendeAEspecie_FicaDeFora()
    {
        var (soGatos, _) = Candidato("Dr. Felino", "44444-SP", 1, "Felino");

        NaRegiao(soGatos);

        var candidatos = await CriarServico().SugerirCandidatosAsync(_consulta.Id);

        // Encaminhar um cao para quem so atende gatos nao e sugestao pior, e errada
        Assert.Empty(candidatos);
    }

    [Fact]
    public async Task Candidatos_VeterinarioDesativado_FicaDeFora()
    {
        var (desativado, _) = Candidato("Dr. Fora", "55555-SP", 1, "Canino");
        desativado.Desativar();

        NaRegiao(desativado);

        Assert.Empty(await CriarServico().SugerirCandidatosAsync(_consulta.Id));
    }

    [Fact]
    public async Task Candidatos_SemHorarioLivre_FicaDeFora()
    {
        var (semAgenda, slot) = Candidato("Dr. Cheio", "66666-SP", 1, "Canino");
        slot.Bloquear();

        NaRegiao(semAgenda);

        Assert.Empty(await CriarServico().SugerirCandidatosAsync(_consulta.Id));
    }

    [Fact]
    public async Task Candidatos_DeConsultaJaRealizada_NaoFazSentido()
    {
        _consulta.Finalizar();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().SugerirCandidatosAsync(_consulta.Id));

        Assert.Equal("RN-025", ex.Codigo);
    }

    // ── Redistribuição (RN-025) ──────────────────────────────────────────────

    [Fact]
    public async Task Redistribuicao_PassaAConsultaAoNovoProfissional()
    {
        var (novo, slot) = Candidato("Dra. Nova", "77777-SP", 3, "Canino");

        var resultado = await CriarServico().RedistribuirAsync(_consulta.Id, Pedido(novo.Id, slot.Id));

        Assert.Equal(novo.Id, _consulta.VeterinarioId);
        Assert.Equal(slot.Inicio, _consulta.DataHora);
        Assert.Equal(_quemSai.Id, resultado.VeterinarioAnteriorId);
    }

    [Fact]
    public async Task Redistribuicao_PreservaOPagamentoDoResponsavel()
    {
        var (novo, slot) = Candidato("Dra. Nova", "77777-SP", 3, "Canino");

        await CriarServico().RedistribuirAsync(_consulta.Id, Pedido(novo.Id, slot.Id));

        // O Responsavel agendou de boa-fe: refazer tudo incluiria pagar de novo
        Assert.Equal(StatusPagamento.Confirmado, _consulta.StatusPagamento);
        Assert.Equal(StatusConsulta.Confirmada, _consulta.Status);
        Assert.Equal(_animal.Id, _consulta.AnimalId);
    }

    [Fact]
    public async Task Redistribuicao_TravaOHorarioNovoELiberaOAntigo()
    {
        var (novo, slot) = Candidato("Dra. Nova", "77777-SP", 3, "Canino");

        await CriarServico().RedistribuirAsync(_consulta.Id, Pedido(novo.Id, slot.Id));

        // Sem travar, duas redistribuicoes simultaneas mandariam dois animais para o
        // mesmo slot
        Assert.Equal(EstadoSlot.Confirmado, slot.Estado);

        // O horario antigo e vaga que alguem pode usar
        Assert.Equal(EstadoSlot.Livre, _slotOriginal.Estado);
    }

    [Fact]
    public async Task Redistribuicao_AvisaOResponsavel()
    {
        var (novo, slot) = Candidato("Dra. Nova", "77777-SP", 3, "Canino");

        CriarNotificacaoDto? aviso = null;
        _notificacoes.Setup(n => n.CriarAsync(It.IsAny<CriarNotificacaoDto>()))
            .Callback<CriarNotificacaoDto>(d => aviso = d)
            .ReturnsAsync(new NotificacaoDto());

        var resultado = await CriarServico().RedistribuirAsync(_consulta.Id, Pedido(novo.Id, slot.Id));

        // Redistribuir sem avisar seria trocar o profissional de alguem sem contar
        Assert.True(resultado.ResponsavelNotificado);
        Assert.Equal(_animal.TutorId, aviso!.TutorId);
        Assert.Contains("Dra. Nova", aviso.Corpo);

        // O motivo entra na mensagem: aviso sem motivo soa como erro do app
        Assert.Contains("encerrou o cadastro", aviso.Corpo);
    }

    [Fact]
    public async Task Redistribuicao_ParaVeterinarioDesativado_NaoEPermitida()
    {
        var (desativado, slot) = Candidato("Dr. Fora", "88888-SP", 1, "Canino");
        desativado.Desativar();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().RedistribuirAsync(_consulta.Id, Pedido(desativado.Id, slot.Id)));

        Assert.Equal("RN-025", ex.Codigo);
    }

    [Fact]
    public async Task Redistribuicao_ParaQuemNaoAtendeAEspecie_NaoEPermitida()
    {
        var (soGatos, slot) = Candidato("Dr. Felino", "99999-SP", 1, "Felino");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().RedistribuirAsync(_consulta.Id, Pedido(soGatos.Id, slot.Id)));

        Assert.Equal("RN-029", ex.Codigo);
    }

    [Fact]
    public async Task Redistribuicao_ComHorarioDeOutroVeterinario_NaoEAceita()
    {
        var (novo, _) = Candidato("Dra. Nova", "77777-SP", 3, "Canino");
        var (outro, slotDoOutro) = Candidato("Dr. Outro", "11111-SP", 4, "Canino");

        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().RedistribuirAsync(_consulta.Id, Pedido(novo.Id, slotDoOutro.Id)));

        Assert.NotEqual(outro.Id, _consulta.VeterinarioId);
    }

    [Fact]
    public async Task Redistribuicao_ComHorarioJaOcupado_Retorna409()
    {
        var (novo, slot) = Candidato("Dra. Nova", "77777-SP", 3, "Canino");
        slot.TravarParaCheckout(Guid.NewGuid(), DateTime.UtcNow);

        await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().RedistribuirAsync(_consulta.Id, Pedido(novo.Id, slot.Id)));
    }

    [Fact]
    public async Task Redistribuicao_ParaOMesmoVeterinario_NaoFazSentido()
    {
        var slot = new Slot(_quemSai.Id, _consulta.DataHora.AddHours(2), _consulta.DataHora.AddHours(2).AddMinutes(30));
        _agendaRepo.Setup(r => r.ObterSlotAsync(slot.Id)).ReturnsAsync(slot);
        _quemSai.AdicionarEspecie("Canino");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarServico().RedistribuirAsync(_consulta.Id, Pedido(_quemSai.Id, slot.Id)));
    }

    [Fact]
    public async Task Redistribuicao_PorQuemNaoEAdmin_ERecusada()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RedistribuirAsync(_consulta.Id, Pedido(Guid.NewGuid(), Guid.NewGuid())));

        // Nem o veterinario que sai decide para quem vai
        Assert.Equal("RN-106", ex.Codigo);
    }

    [Fact]
    public async Task Redistribuicao_ZeraOVinculoComACapturaAnterior()
    {
        _consulta.RegistrarInicio(DateTime.UtcNow.AddHours(-1));

        var (novo, slot) = Candidato("Dra. Nova", "77777-SP", 3, "Canino");

        await CriarServico().RedistribuirAsync(_consulta.Id, Pedido(novo.Id, slot.Id));

        // O novo profissional comeca do zero: redistribuir nao e iniciar de novo
        Assert.Null(_consulta.IniciadaEm);
    }
}
