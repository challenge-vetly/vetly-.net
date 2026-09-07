using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Painel consolidado da unidade (§5.2, §7.3).
///
/// O produto diz que "o administrador visualiza o dashboard consolidado da unidade,
/// com a agenda de todos os veterinarios e os indicadores operacionais do
/// estabelecimento". O que estes testes travam e isso mais o que a §7.3 proibe: a
/// unidade sai do vinculo do proprio Admin, e nao de um id que o cliente escolhe.
/// </summary>
public class DashboardDaUnidadeTests
{
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IDocumentoRepository> _documentoRepo = new();
    private readonly Mock<ICapturaRepository> _capturaRepo = new();
    private readonly Mock<IAvaliacaoRepository> _avaliacaoRepo = new();
    private readonly Mock<ILembreteRepository> _lembreteRepo = new();
    private readonly Mock<IEmpresaRepository> _empresaRepo = new();
    private readonly Mock<IAgendaRepository> _agendaRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Veterinario _admin;
    private readonly Empresa _empresa;

    public DashboardDaUnidadeTests()
    {
        _admin = new Veterinario("Dr. Paulo", new Crmv("11111-SP"), "SP",
            PersonaVeterinario.Vinculado, PlanoAssinatura.Enterprise);

        _empresa = new Empresa("Clinica Vetly Centro", "Clinica", _admin.Id, PlanoAssinatura.Enterprise);

        _usuario.SetupGet(u => u.EhAdmin).Returns(true);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(_admin.Id);

        _empresaRepo.Setup(r => r.ObterPorAdministradorAsync(_admin.Id)).ReturnsAsync([_empresa]);
        _vetRepo.Setup(r => r.ObterPorEmpresaAsync(_empresa.Id)).ReturnsAsync([]);

        _consultaRepo.Setup(r => r.ObterPorVeterinarioAsync(
            It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync([]);

        _agendaRepo.Setup(r => r.ObterSlotsAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        _lembreteRepo.Setup(r => r.ObterEscaladosParaClinicaAsync(It.IsAny<DateTime>())).ReturnsAsync([]);

        _consultaRepo.Setup(r => r.ObterAnimaisAtendidosAsync(
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<IEnumerable<Guid>>())).ReturnsAsync([]);
    }

    private DashboardService CriarServico() =>
        new(_consultaRepo.Object, _vetRepo.Object, _animalRepo.Object, _pagamentoRepo.Object,
            _documentoRepo.Object, _capturaRepo.Object, _avaliacaoRepo.Object, _lembreteRepo.Object,
            _empresaRepo.Object, _agendaRepo.Object, _usuario.Object);

    /// <summary>Vet vinculado com um dia de agenda e uma consulta no estado pedido.</summary>
    private Veterinario VinculadoComAgenda(
        string nome, DateTime dia, int slots, int ocupados, StatusConsulta? statusDaConsulta = null)
    {
        var vet = new Veterinario(nome, new Crmv("22222-SP"), "SP",
            PersonaVeterinario.Vinculado, PlanoAssinatura.Enterprise);

        vet.VincularEmpresa(_empresa.Id);

        var materializados = new List<Slot>();

        for (var i = 0; i < slots; i++)
        {
            var slot = new Slot(vet.Id, dia.AddHours(9 + i), dia.AddHours(9 + i).AddMinutes(30));

            if (i < ocupados)
            {
                slot.TravarParaCheckout(Guid.NewGuid(), dia);
                slot.Confirmar();
            }

            materializados.Add(slot);
        }

        _agendaRepo.Setup(r => r.ObterSlotsAsync(vet.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(materializados);

        if (statusDaConsulta is { } status)
        {
            var consulta = ConsultaNoStatus(vet.Id, dia.AddHours(9), status);

            _consultaRepo.Setup(r => r.ObterPorVeterinarioAsync(
                vet.Id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync([consulta]);
        }

        return vet;
    }

    private Consulta ConsultaNoStatus(Guid vetId, DateTime quando, StatusConsulta status)
    {
        var animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());
        _animalRepo.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);

        var consulta = new Consulta(quando, ModalidadeAtendimento.Presencial,
            animal.TutorId, animal.Id, vetId);

        switch (status)
        {
            case StatusConsulta.Realizada: consulta.Finalizar(); break;
            case StatusConsulta.Cancelada: consulta.Cancelar(); break;
            case StatusConsulta.NoShow: consulta.RegistrarNoShow(); break;
        }

        return consulta;
    }

    // ── Escopo (§7.3, RN-106) ────────────────────────────────────────────────

    [Fact]
    public async Task NaoAdmin_ENegado()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().ObterDaUnidadeAsync(null));
    }

    [Fact]
    public async Task AdminSemUnidadeAdministrada_NaoRecebePainelDeNinguem()
    {
        // A §7.3 veda "dados de outros estabelecimentos": sem vinculo, nao ha painel a
        // devolver — e nao ha id na rota por onde pedir o de outra clinica.
        _empresaRepo.Setup(r => r.ObterPorAdministradorAsync(_admin.Id)).ReturnsAsync([]);

        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().ObterDaUnidadeAsync(null));
    }

    [Fact]
    public async Task UnidadeVemDoVinculoDoProprioAdmin()
    {
        var painel = await CriarServico().ObterDaUnidadeAsync(null);

        Assert.Equal(_empresa.Id, painel.EmpresaId);
        Assert.Equal("Clinica Vetly Centro", painel.Nome);
    }

    // ── Agenda de todos os profissionais (§5.2) ──────────────────────────────

    [Fact]
    public async Task TrazUmBlocoPorProfissionalVinculado()
    {
        var dia = DateTime.UtcNow.Date;

        var a = VinculadoComAgenda("Dra. Marina", dia, slots: 4, ocupados: 1);
        var b = VinculadoComAgenda("Dr. Caio", dia, slots: 2, ocupados: 2);

        _vetRepo.Setup(r => r.ObterPorEmpresaAsync(_empresa.Id)).ReturnsAsync([a, b]);

        var painel = await CriarServico().ObterDaUnidadeAsync(dia);

        Assert.Equal(2, painel.Profissionais.Count);
        Assert.Equal(6, painel.Indicadores.HorariosNoDia);
        Assert.Equal(3, painel.Indicadores.HorariosOcupados);
        Assert.Equal(50m, painel.Indicadores.TaxaDeOcupacao);
    }

    [Fact]
    public async Task ProfissionalDesativado_ContinuaNoPainelMarcadoComoInativo()
    {
        // A agenda dele pode ter atendimento futuro a redistribuir (RN-025): sumir com
        // o profissional esconderia justamente o que o administrador precisa resolver.
        var dia = DateTime.UtcNow.Date;
        var vet = VinculadoComAgenda("Dra. Marina", dia, slots: 2, ocupados: 0);
        vet.Desativar();

        _vetRepo.Setup(r => r.ObterPorEmpresaAsync(_empresa.Id)).ReturnsAsync([vet]);

        var painel = await CriarServico().ObterDaUnidadeAsync(dia);

        Assert.Single(painel.Profissionais);
        Assert.False(painel.Profissionais[0].Ativo);
        Assert.Equal(0, painel.Indicadores.ProfissionaisAtivos);
    }

    [Fact]
    public async Task SemAgendaMaterializada_OcupacaoEZeroENaoCemPorCento()
    {
        // Divisao por zero mal tratada faria a clinica que nem configurou agenda
        // aparecer lotada.
        var vet = VinculadoComAgenda("Dra. Marina", DateTime.UtcNow.Date, slots: 0, ocupados: 0);
        _vetRepo.Setup(r => r.ObterPorEmpresaAsync(_empresa.Id)).ReturnsAsync([vet]);

        var painel = await CriarServico().ObterDaUnidadeAsync(null);

        Assert.Equal(0m, painel.Indicadores.TaxaDeOcupacao);
    }

    // ── Indicadores operacionais (§5.2) ──────────────────────────────────────

    [Theory]
    [InlineData(StatusConsulta.Realizada, 1, 0, 0, 1)]
    [InlineData(StatusConsulta.Cancelada, 0, 1, 0, 0)]
    [InlineData(StatusConsulta.NoShow, 0, 0, 1, 1)]
    public async Task ContaCadaDesfechoNaColunaCerta(
        StatusConsulta status, int realizados, int cancelados, int noShow, int noDia)
    {
        var dia = DateTime.UtcNow.Date;
        var vet = VinculadoComAgenda("Dra. Marina", dia, slots: 1, ocupados: 1, statusDaConsulta: status);

        _vetRepo.Setup(r => r.ObterPorEmpresaAsync(_empresa.Id)).ReturnsAsync([vet]);

        var painel = await CriarServico().ObterDaUnidadeAsync(dia);

        Assert.Equal(realizados, painel.Indicadores.Realizados);
        Assert.Equal(cancelados, painel.Indicadores.Cancelados);
        Assert.Equal(noShow, painel.Indicadores.NoShow);

        // Cancelada nao e atendimento do dia: conta-la inflaria a ocupacao da unidade
        Assert.Equal(noDia, painel.Indicadores.AtendimentosNoDia);
    }

    // ── Alerta da regua (RN-095, §6.4) ───────────────────────────────────────

    [Fact]
    public async Task ReguaEscalada_ApareceParaAUnidadeQueAtendeuOAnimal()
    {
        var dia = DateTime.UtcNow.Date;
        var animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());
        _animalRepo.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);

        var vet = new Veterinario("Dra. Marina", new Crmv("22222-SP"), "SP",
            PersonaVeterinario.Vinculado, PlanoAssinatura.Enterprise);
        vet.VincularEmpresa(_empresa.Id);

        _vetRepo.Setup(r => r.ObterPorEmpresaAsync(_empresa.Id)).ReturnsAsync([vet]);

        _consultaRepo.Setup(r => r.ObterAnimaisAtendidosAsync(
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync([animal.Id]);

        _lembreteRepo.Setup(r => r.ObterEscaladosParaClinicaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([ReguaEsgotada(animal.Id, animal.TutorId)]);

        var painel = await CriarServico().ObterDaUnidadeAsync(dia);

        Assert.Single(painel.ResponsaveisNaoResponsivos);
        Assert.Equal("Thor", painel.ResponsaveisNaoResponsivos[0].AnimalNome);
        Assert.Equal(TipoLembrete.Retorno, painel.ResponsaveisNaoResponsivos[0].Tipo);
    }

    [Fact]
    public async Task ReguaDeAnimalQueAUnidadeNuncaAtendeu_NaoAparece()
    {
        // Sem o recorte por animal atendido, cada clinica veria os Responsaveis
        // omissos de todas as outras.
        var vet = VinculadoComAgenda("Dra. Marina", DateTime.UtcNow.Date, slots: 0, ocupados: 0);
        _vetRepo.Setup(r => r.ObterPorEmpresaAsync(_empresa.Id)).ReturnsAsync([vet]);

        _lembreteRepo.Setup(r => r.ObterEscaladosParaClinicaAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([ReguaEsgotada(Guid.NewGuid(), Guid.NewGuid())]);

        var painel = await CriarServico().ObterDaUnidadeAsync(null);

        Assert.Empty(painel.ResponsaveisNaoResponsivos);
    }

    /// <summary>Régua com as três tentativas gastas — o estado que aciona a clínica.</summary>
    private static LembreteAgendado ReguaEsgotada(Guid animalId, Guid tutorId)
    {
        var lembrete = new LembreteAgendado(
            animalId, tutorId, TipoLembrete.Retorno, DateTime.UtcNow.AddDays(-3));

        lembrete.RegistrarTentativa();
        lembrete.RegistrarTentativa();
        lembrete.RegistrarTentativa();

        return lembrete;
    }
}
