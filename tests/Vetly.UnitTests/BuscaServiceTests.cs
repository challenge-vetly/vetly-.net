using Moq;
using Vetly.Application.DTOs.Busca;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes do matching por geolocalizacao (RN-001 a RN-033).
/// Cobre o filtro eliminatorio de especie, o raio, o score 40/30/30, a
/// renormalizacao de quem nao tem nota (P-09) e o desempate da RN-031.
/// </summary>
public class BuscaServiceTests
{
    private readonly Mock<IBuscaRepository> _buscaRepo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IAgendaRepository> _agendaRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    // Av. Paulista, ponto de referencia dos testes
    private const decimal LatOrigem = -23.561414m;
    private const decimal LngOrigem = -46.655881m;

    private readonly Animal _animal;

    public BuscaServiceTests()
    {
        _animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());
        _animalRepo.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync(_animal);
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        _agendaRepo.Setup(r => r.ContarDisponiveisNasProximas48hAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
        _agendaRepo.Setup(r => r.ObterProximoHorarioLivreAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateTime>()))
            .ReturnsAsync([]);
    }

    private BuscaService CriarServico() =>
        new(_buscaRepo.Object, _animalRepo.Object, _agendaRepo.Object, _usuario.Object);

    /// <summary>Cria um vet autonomo publicado, pronto para aparecer na busca.</summary>
    private static Veterinario Autonomo(
        string nome, decimal lat, decimal lng,
        string especie = "Canino", decimal nota = 0, int numAvaliacoes = 0,
        DateTime? publicadoEm = null, string? especialidade = null)
    {
        var vet = new Veterinario(nome, new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        var endereco = new Endereco("01310-100", "Av. Paulista", "1578", "Bela Vista", "Sao Paulo", "SP");
        endereco.DefinirCoordenada(lat, lng);
        vet.DefinirEndereco(endereco);
        vet.AdicionarEspecie(especie);

        if (especialidade is not null) vet.AdicionarEspecialidade(especialidade);

        vet.RegistrarValidacaoCrmv(StatusCrmv.Valido, DateTime.UtcNow);
        vet.PublicarNoMatching(publicadoEm ?? DateTime.UtcNow.AddYears(-1));

        if (numAvaliacoes > 0) vet.AtualizarReputacao(nota, numAvaliacoes);

        return vet;
    }

    private void RepositorioDevolve(
        IEnumerable<Veterinario>? autonomos = null,
        IEnumerable<Empresa>? empresas = null,
        Dictionary<Guid, List<Veterinario>>? vinculados = null,
        Dictionary<Guid, List<Servico>>? servicos = null)
    {
        _buscaRepo
            .Setup(r => r.ObterCandidatosAsync(
                It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>(), It.IsAny<decimal>()))
            .ReturnsAsync(new CandidatosDoMatching(
                [.. autonomos ?? []],
                [.. empresas ?? []],
                vinculados ?? [],
                servicos ?? []));
    }

    private static FiltroBuscaDto Filtro(double? raio = null) => new()
    {
        AnimalId = Guid.NewGuid(),
        Lat = LatOrigem,
        Lng = LngOrigem,
        RaioKm = raio
    };

    // ── Filtro eliminatório de espécie (RN-029) ──────────────────────────────

    [Fact]
    public async Task Busca_VetQueNaoAtendeAEspecie_NaoAparece()
    {
        RepositorioDevolve([Autonomo("So Felinos", LatOrigem, LngOrigem, especie: "Felino")]);

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());

        // Matching clinicamente invalido nao pode aparecer nem no fim da lista
        Assert.Empty(resultado.Itens);
        Assert.Equal("Canino", resultado.EspecieDoAnimal);
    }

    [Fact]
    public async Task Busca_VetQueAtendeAEspecie_Aparece()
    {
        RepositorioDevolve([Autonomo("Atende Caes", LatOrigem, LngOrigem)]);

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());

        Assert.Single(resultado.Itens);
    }

    // ── Raio (RN-028) ────────────────────────────────────────────────────────

    [Fact]
    public async Task Busca_ForaDoRaio_NaoAparece()
    {
        // ~11 km ao norte da origem
        RepositorioDevolve([Autonomo("Longe", LatOrigem + 0.10m, LngOrigem)]);

        var resultado = await CriarServico().BuscarAsync(Filtro(raio: 5), new Paginacao());

        Assert.Empty(resultado.Itens);
    }

    [Fact]
    public async Task Busca_SemRaioInformado_UsaDezQuilometros()
    {
        RepositorioDevolve([Autonomo("Perto", LatOrigem, LngOrigem)]);

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());

        Assert.Equal(10, resultado.RaioAplicadoKm);
    }

    [Fact]
    public async Task Busca_RaioAcimaDoMaximo_ELimitadoAVinteECinco()
    {
        RepositorioDevolve([Autonomo("Perto", LatOrigem, LngOrigem)]);

        var resultado = await CriarServico().BuscarAsync(
            new FiltroBuscaDto { AnimalId = Guid.NewGuid(), Lat = LatOrigem, Lng = LngOrigem, RaioKm = 100 },
            new Paginacao());

        Assert.Equal(25, resultado.RaioAplicadoKm);
    }

    // ── Posição e fallback (RN-027) ──────────────────────────────────────────

    [Fact]
    public async Task Busca_SemLocalizacaoNemCep_NaoProssegue()
    {
        RepositorioDevolve();

        await Assert.ThrowsAsync<ValidationException>(() =>
            CriarServico().BuscarAsync(new FiltroBuscaDto { AnimalId = Guid.NewGuid() }, new Paginacao()));
    }

    [Fact]
    public async Task Busca_ComCep_UsaOFallbackEInformaAOrigem()
    {
        _buscaRepo.Setup(r => r.ObterCoordenadaDoCepAsync("01310-100"))
            .ReturnsAsync((LatOrigem, LngOrigem));
        RepositorioDevolve([Autonomo("Perto", LatOrigem, LngOrigem)]);

        var resultado = await CriarServico().BuscarAsync(
            new FiltroBuscaDto { AnimalId = Guid.NewGuid(), Cep = "01310-100" }, new Paginacao());

        // Sem o fallback o fluxo de busca travaria quando a permissao e negada
        Assert.Equal(OrigemDaPosicao.Cep, resultado.Origem);
        Assert.Single(resultado.Itens);
    }

    [Fact]
    public async Task Busca_ComCepDesconhecido_ExplicaOQueFazer()
    {
        _buscaRepo.Setup(r => r.ObterCoordenadaDoCepAsync(It.IsAny<string>()))
            .ReturnsAsync(((decimal, decimal)?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CriarServico().BuscarAsync(
                new FiltroBuscaDto { AnimalId = Guid.NewGuid(), Cep = "99999-999" }, new Paginacao()));

        Assert.Equal("RN-027", ex.Codigo);
    }

    // ── Score e ordenação (RN-030, RN-031, RN-033, RN-057, P-09) ─────────────

    [Fact]
    public async Task Score_MaisPertoVemAntes_QuandoTudoMaisEIgual()
    {
        var perto = Autonomo("Perto", LatOrigem, LngOrigem);
        var longe = Autonomo("Longe", LatOrigem + 0.05m, LngOrigem);   // ~5,5 km
        RepositorioDevolve([longe, perto]);

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());

        Assert.Equal("Perto", resultado.Itens[0].Nome);
        Assert.True(resultado.Itens[0].Score > resultado.Itens[1].Score);
    }

    [Fact]
    public async Task Nota_SoEPublicaApartirDeTresAvaliacoes()
    {
        var comDuas = Autonomo("Duas avaliacoes", LatOrigem, LngOrigem, nota: 5m, numAvaliacoes: 2);
        var comTres = Autonomo("Tres avaliacoes", LatOrigem, LngOrigem, nota: 4m, numAvaliacoes: 3);
        RepositorioDevolve([comDuas, comTres]);

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());

        var duas = resultado.Itens.Single(i => i.Nome == "Duas avaliacoes");
        var tres = resultado.Itens.Single(i => i.Nome == "Tres avaliacoes");

        // Abaixo de tres, uma unica nota extrema definiria o perfil inteiro (RN-057)
        Assert.Null(duas.Nota);
        Assert.Equal(4m, tres.Nota);
    }

    [Fact]
    public async Task SemNota_OScoreERenormalizadoEntreDistanciaEDisponibilidade()
    {
        RepositorioDevolve([Autonomo("Novato", LatOrigem, LngOrigem)]);

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());
        var composicao = resultado.Itens[0].Composicao;

        // P-09: sem nota, os pesos de distancia e disponibilidade sao renormalizados
        // (57/43). Sem isso o entrante competiria com 30% do score zerado — a punicao
        // que a RN-033 quer evitar.
        Assert.Equal(0, composicao.PesoAvaliacao);
        Assert.Equal(0.5714, composicao.PesoDistancia, 3);
        Assert.Equal(0.4286, composicao.PesoDisponibilidade, 3);
    }

    [Fact]
    public async Task ComNota_OsPesosSao40Por30Por30()
    {
        RepositorioDevolve([Autonomo("Avaliado", LatOrigem, LngOrigem, nota: 4.5m, numAvaliacoes: 10)]);

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());
        var composicao = resultado.Itens[0].Composicao;

        Assert.Equal(0.40, composicao.PesoDistancia);
        Assert.Equal(0.30, composicao.PesoAvaliacao);
        Assert.Equal(0.30, composicao.PesoDisponibilidade);
    }

    [Fact]
    public async Task SeloNovo_ValePorTrintaDiasEnquantoNaoHaNota()
    {
        var recente = Autonomo("Recem-chegado", LatOrigem, LngOrigem, publicadoEm: DateTime.UtcNow.AddDays(-10));
        var antigo = Autonomo("Antigo sem nota", LatOrigem, LngOrigem, publicadoEm: DateTime.UtcNow.AddDays(-60));
        var avaliado = Autonomo("Avaliado recente", LatOrigem, LngOrigem,
            nota: 4m, numAvaliacoes: 5, publicadoEm: DateTime.UtcNow.AddDays(-10));
        RepositorioDevolve([recente, antigo, avaliado]);

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());

        Assert.True(resultado.Itens.Single(i => i.Nome == "Recem-chegado").SeloNovo);
        Assert.False(resultado.Itens.Single(i => i.Nome == "Antigo sem nota").SeloNovo);
        // Quem ja tem nota nao precisa do selo: ele substitui a nota que falta (RN-033)
        Assert.False(resultado.Itens.Single(i => i.Nome == "Avaliado recente").SeloNovo);
    }

    [Fact]
    public async Task Disponibilidade_EntraNoScore()
    {
        var comAgenda = Autonomo("Com agenda", LatOrigem, LngOrigem);
        var semAgenda = Autonomo("Sem agenda", LatOrigem, LngOrigem);
        RepositorioDevolve([semAgenda, comAgenda]);

        _agendaRepo.Setup(r => r.ContarDisponiveisNasProximas48hAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<Guid, int> { [comAgenda.Id] = 10 });

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());

        Assert.Equal("Com agenda", resultado.Itens[0].Nome);
        Assert.Equal(10, resultado.Itens[0].HorariosLivres48h);
    }

    // ── Filtros de serviço (RN-032) ──────────────────────────────────────────

    [Fact]
    public async Task Necessidade_ProvedorSemOServico_NaoAparece()
    {
        var comBanho = Autonomo("So banho", LatOrigem, LngOrigem);
        RepositorioDevolve(
            [comBanho],
            servicos: new Dictionary<Guid, List<Servico>>
            {
                [comBanho.Id] = [new Servico(comBanho.Id, TipoServico.Banho, 80m, 60)]
            });

        var filtro = Filtro();
        filtro.Necessidade = TipoServico.Cirurgia;

        var resultado = await CriarServico().BuscarAsync(filtro, new Paginacao());

        Assert.Empty(resultado.Itens);
    }

    [Fact]
    public async Task Necessidade_TrazOValorDoServico()
    {
        var vet = Autonomo("Consulta", LatOrigem, LngOrigem);
        RepositorioDevolve(
            [vet],
            servicos: new Dictionary<Guid, List<Servico>>
            {
                [vet.Id] = [new Servico(vet.Id, TipoServico.ConsultaRotina, 200m, 30)]
            });

        var filtro = Filtro();
        filtro.Necessidade = TipoServico.ConsultaRotina;

        var resultado = await CriarServico().BuscarAsync(filtro, new Paginacao());

        Assert.Equal(200m, resultado.Itens[0].ValorServico);
    }

    [Fact]
    public async Task FaixaDePreco_FiltraForaDoIntervalo()
    {
        var barato = Autonomo("Barato", LatOrigem, LngOrigem);
        var caro = Autonomo("Caro", LatOrigem, LngOrigem);
        RepositorioDevolve(
            [barato, caro],
            servicos: new Dictionary<Guid, List<Servico>>
            {
                [barato.Id] = [new Servico(barato.Id, TipoServico.ConsultaRotina, 120m, 30)],
                [caro.Id] = [new Servico(caro.Id, TipoServico.ConsultaRotina, 400m, 30)]
            });

        var filtro = Filtro();
        filtro.Necessidade = TipoServico.ConsultaRotina;
        filtro.ValorMaximo = 200m;

        var resultado = await CriarServico().BuscarAsync(filtro, new Paginacao());

        Assert.Single(resultado.Itens);
        Assert.Equal("Barato", resultado.Itens[0].Nome);
    }

    [Fact]
    public async Task Especialidade_FiltraQuemNaoTem()
    {
        var ortopedista = Autonomo("Ortopedista", LatOrigem, LngOrigem, especialidade: "Ortopedia");
        var generalista = Autonomo("Generalista", LatOrigem, LngOrigem);
        RepositorioDevolve([ortopedista, generalista]);

        var filtro = Filtro();
        filtro.Especialidade = "Ortopedia";

        var resultado = await CriarServico().BuscarAsync(filtro, new Paginacao());

        Assert.Single(resultado.Itens);
        Assert.Equal("Ortopedista", resultado.Itens[0].Nome);
    }

    /// <remarks>
    /// "Hoje" e o dia do calendario em UTC, como todo o resto da plataforma (§2.3).
    /// Para um Responsavel em fuso distante, a virada do dia dele nao coincide com a
    /// do filtro — vale registrar quando a localizacao do usuario entrar no produto.
    /// </remarks>
    [Fact]
    public async Task AtendeHoje_DeixaDeForaQuemSoTemHorarioAmanha()
    {
        var hoje = Autonomo("Atende hoje", LatOrigem, LngOrigem);
        var amanha = Autonomo("So amanha", LatOrigem, LngOrigem);
        RepositorioDevolve([hoje, amanha]);

        _agendaRepo.Setup(r => r.ObterProximoHorarioLivreAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<DateTime>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>
            {
                // Fim do dia de hoje e inicio de depois de amanha: horarios ancorados na
                // data, nao em "daqui a N horas", que viraria outro dia perto da meia-noite
                [hoje.Id] = DateTime.UtcNow.Date.AddHours(23).AddMinutes(59),
                [amanha.Id] = DateTime.UtcNow.Date.AddDays(2).AddHours(9)
            });

        var filtro = Filtro();
        filtro.AtendeHoje = true;

        var resultado = await CriarServico().BuscarAsync(filtro, new Paginacao());

        Assert.Single(resultado.Itens);
        Assert.Equal("Atende hoje", resultado.Itens[0].Nome);
    }

    // ── Clínicas (RN-003) ────────────────────────────────────────────────────

    [Fact]
    public async Task Clinica_HerdaEspecieENotaDaEquipe()
    {
        var empresa = new Empresa("Clinica Vida Pet", "Clinica", Guid.NewGuid());
        var endereco = new Endereco("01310-100", "Av. Paulista", "1578", "Bela Vista", "Sao Paulo", "SP");
        endereco.DefinirCoordenada(LatOrigem, LngOrigem);
        empresa.DefinirEndereco(endereco);

        var daEquipe = Autonomo("Vet da clinica", LatOrigem, LngOrigem, nota: 4.8m, numAvaliacoes: 20);

        RepositorioDevolve(
            empresas: [empresa],
            vinculados: new Dictionary<Guid, List<Veterinario>> { [empresa.Id] = [daEquipe] });

        var resultado = await CriarServico().BuscarAsync(Filtro(), new Paginacao());

        var clinica = resultado.Itens.Single();
        Assert.Equal(TipoPrestador.Empresa, clinica.Tipo);
        Assert.Equal("Clinica Vida Pet", clinica.Nome);
        Assert.Contains("Canino", clinica.EspeciesAtendidas);
        Assert.Equal(4.8m, clinica.Nota);
        Assert.Equal(20, clinica.NumAvaliacoes);
    }

    // ── Escopo (RN-105) ──────────────────────────────────────────────────────

    [Fact]
    public async Task Busca_ComAnimalDeOutroResponsavel_ERecusada()
    {
        _usuario.SetupGet(u => u.EhAdmin).Returns(false);
        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());
        RepositorioDevolve();

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            CriarServico().BuscarAsync(Filtro(), new Paginacao()));

        Assert.Equal("RN-105", ex.Codigo);
    }
}
