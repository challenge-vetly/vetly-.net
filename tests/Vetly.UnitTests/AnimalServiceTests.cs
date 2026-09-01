using Moq;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do AnimalService.
/// Cobre o perfil clinico do pet — peso obrigatorio (RN-081), alergias, condicoes
/// pre-existentes e carteira de vacinacao (RN-046).
/// </summary>
public class AnimalServiceTests
{
    private readonly Mock<IAnimalRepository> _repoMock = new();
    private readonly Mock<IColmeiaService> _colmeiaMock = new();
    private readonly Mock<IUsuarioAtual> _usuarioMock = new();

    private readonly Mock<IObrigacaoService> _obrigacoesMock = new();
    private readonly Mock<IDocumentoRepository> _documentoRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();

    private AnimalService CriarServico() =>
        new(_repoMock.Object, _colmeiaMock.Object, _obrigacoesMock.Object,
            _documentoRepoMock.Object, _vetRepoMock.Object, _usuarioMock.Object);

    /// <summary>Por padrao os testes rodam como Admin, que enxerga todo o escopo.</summary>
    public AnimalServiceTests() => _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

    private static CriarAnimalDto CriarDto(decimal pesoKg = 31.5m) => new()
    {
        Nome = "Thor",
        Especie = "Canino",
        Raca = "Golden Retriever",
        DataNascimento = new DateTime(2023, 4, 10, 0, 0, 0, DateTimeKind.Utc),
        TutorId = Guid.NewGuid(),
        PesoKg = pesoKg,
        Sexo = SexoAnimal.Macho,
        Castrado = true,
        Alergias = ["Dipirona"],
        CondicoesPreexistentes = ["Displasia leve"],
        CarteiraVacinacao =
        [
            new RegistroVacinacaoDto { Tipo = "V10", AplicadaEm = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc) }
        ]
    };

    [Fact]
    public async Task CriarAsync_ComPerfilClinicoCompleto_PersisteTodosOsCampos()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Animal>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().CriarAsync(CriarDto());

        Assert.Equal(31.5m, resultado.PesoKg);
        Assert.Equal(SexoAnimal.Macho, resultado.Sexo);
        Assert.True(resultado.Castrado);
        Assert.Equal(["Dipirona"], resultado.Alergias);
        Assert.Equal(["Displasia leve"], resultado.CondicoesPreexistentes);
        Assert.Single(resultado.CarteiraVacinacao);
        Assert.Equal("V10", resultado.CarteiraVacinacao[0].Tipo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    public async Task CriarAsync_PesoNaoPositivo_LancaBusinessRuleExceptionRN081(decimal peso)
    {
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().CriarAsync(CriarDto(peso)));

        Assert.Equal("RN-081", ex.Codigo);
        // Nada e persistido quando o peso nao passa na guarda
        _repoMock.Verify(r => r.AdicionarAsync(It.IsAny<Animal>()), Times.Never);
    }

    [Fact]
    public async Task AtualizarAsync_AlteraPesoEPerfilClinico()
    {
        var animal = new Animal("Thor", "Canino", "Golden Retriever",
            new DateTime(2023, 4, 10, 0, 0, 0, DateTimeKind.Utc), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Animal>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = CriarDto(pesoKg: 34.2m);
        await CriarServico().AtualizarAsync(animal.Id, dto);

        Assert.Equal(34.2m, animal.PesoKg);
        Assert.Equal(SexoAnimal.Macho, animal.Sexo);
        Assert.Contains("Dipirona", animal.Alergias);
    }

    [Fact]
    public void RegistrarPeso_ValorNaoPositivo_LancaArgumentOutOfRange()
    {
        var animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());

        // Invariante do dominio: defesa em profundidade atras da guarda do servico
        Assert.Throws<ArgumentOutOfRangeException>(() => animal.RegistrarPeso(0));
    }

    [Fact]
    public void Animal_RecemCriado_NaoTemPesoNemCarteira()
    {
        var animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());

        Assert.Null(animal.PesoKg);
        Assert.Empty(animal.CarteiraVacinacao);
        Assert.Empty(animal.Alergias);
    }

    // ── RN-105: ler o prontuario nao e reescrever o cadastro ────────────────

    private void ComoVeterinario(Guid veterinarioId)
    {
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhVeterinario).Returns(true);
        _usuarioMock.SetupGet(u => u.VeterinarioId).Returns(veterinarioId);
    }

    private static Animal CriarAnimalDe(Guid tutorId) =>
        new("Thor", "Canino", "Golden Retriever",
            new DateTime(2023, 4, 10, 0, 0, 0, DateTimeKind.Utc), tutorId);

    [Fact]
    public async Task AtualizarAsync_VeterinarioQueAtendeOAnimal_LancaAcessoNegadoRN105()
    {
        var vetId = Guid.NewGuid();
        var animal = CriarAnimalDe(Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, animal.Id)).ReturnsAsync(true);
        ComoVeterinario(vetId);

        var dto = CriarDto();
        dto.Nome = "Rex";

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().AtualizarAsync(animal.Id, dto));

        // Atender uma vez nao da direito de renomear o pet nem trocar a especie:
        // leitura clinica e escrita cadastral sao escopos distintos
        Assert.Equal("RN-105", ex.Codigo);
        Assert.Equal("Thor", animal.Nome);
    }

    [Fact]
    public async Task DesativarAsync_VeterinarioQueAtendeOAnimal_LancaAcessoNegadoRN105()
    {
        var vetId = Guid.NewGuid();
        var animal = CriarAnimalDe(Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, animal.Id)).ReturnsAsync(true);
        ComoVeterinario(vetId);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().DesativarAsync(animal.Id));

        Assert.Equal("RN-105", ex.Codigo);
        Assert.True(animal.Ativo);
    }

    [Fact]
    public async Task AtualizarAsync_TutorDono_Aplica()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimalDe(tutorId);

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Animal>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhTutor).Returns(true);
        _usuarioMock.SetupGet(u => u.TutorId).Returns(tutorId);

        var dto = CriarDto();
        dto.Nome = "Thor Junior";

        await CriarServico().AtualizarAsync(animal.Id, dto);

        Assert.Equal("Thor Junior", animal.Nome);
    }

    // ── RN-081: o peso e a excecao, e tem caminho proprio ───────────────────

    [Fact]
    public async Task RegistrarPesoAsync_VeterinarioQueAtendeOAnimal_Aplica()
    {
        var vetId = Guid.NewGuid();
        var animal = CriarAnimalDe(Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, animal.Id)).ReturnsAsync(true);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Animal>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        ComoVeterinario(vetId);

        var resultado = await CriarServico().RegistrarPesoAsync(animal.Id, 33.2m);

        // Sem peso a IA nao sugere dose: o vet o afere na consulta (RN-081)
        Assert.Equal(33.2m, resultado.PesoKg);
    }

    [Fact]
    public async Task RegistrarPesoAsync_VeterinarioDeFora_LancaAcessoNegadoRN105()
    {
        var vetId = Guid.NewGuid();
        var animal = CriarAnimalDe(Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, animal.Id)).ReturnsAsync(false);
        _colmeiaMock.Setup(c => c.PodeAcessarAsync(vetId, animal.Id, It.IsAny<EscopoAcessoColmeia>()))
            .ReturnsAsync(false);
        ComoVeterinario(vetId);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarPesoAsync(animal.Id, 33.2m));

        Assert.Equal("RN-105", ex.Codigo);
    }

    // ── RN-068: ocultar do board nao e apagar ──────────────────────────────

    private (Animal Animal, Prontuario Registro) CenarioDeHistorico(string dadosClinicos = "Consulta de rotina.")
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimalDe(tutorId);
        var registro = new Prontuario(Guid.NewGuid(), animal.Id, dadosClinicos);

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ObterHistoricoLongitudinalAsync(animal.Id)).ReturnsAsync([registro]);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Animal>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhTutor).Returns(true);
        _usuarioMock.SetupGet(u => u.TutorId).Returns(tutorId);

        return (animal, registro);
    }

    [Fact]
    public async Task OcultarDoHistorico_PeloDono_EscondeSemApagar()
    {
        var (animal, registro) = CenarioDeHistorico();

        var resultado = await CriarServico().DefinirVisibilidadeDoHistoricoAsync(
            animal.Id, registro.Id, oculto: true);

        Assert.True(resultado.Oculto);
        Assert.True(registro.Oculto);

        // O registro continua existindo: a guarda regulatoria do prontuario permanece
        Assert.Equal("Consulta de rotina.", registro.DadosClinicos);
    }

    [Fact]
    public async Task ObterHistorico_ComoTutor_OmiteOQueEleOcultou()
    {
        var (animal, registro) = CenarioDeHistorico();
        registro.DefinirVisibilidade(true);

        var historico = await CriarServico().ObterHistoricoAsync(animal.Id);

        Assert.Empty(historico);
    }

    [Fact]
    public async Task ObterHistorico_ComoAdmin_ContinuaVendoOOculto()
    {
        var (animal, registro) = CenarioDeHistorico();
        registro.DefinirVisibilidade(true);

        _usuarioMock.SetupGet(u => u.EhTutor).Returns(false);
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

        var historico = (await CriarServico().ObterHistoricoAsync(animal.Id)).ToList();

        // Um historico que some da vista de quem prescreve seria perigoso, nao discreto
        Assert.Single(historico);
        Assert.True(historico[0].Oculto);
    }

    [Fact]
    public async Task OcultarDoHistorico_RegistroComAlertaDeSeguranca_LancaBusinessRuleRN068()
    {
        var (animal, registro) = CenarioDeHistorico("Reacao a Dipirona durante o pos-operatorio.");
        animal.DefinirPerfilClinico(alergias: ["Dipirona"]);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().DefinirVisibilidadeDoHistoricoAsync(animal.Id, registro.Id, oculto: true));

        // Esconder uma alergia do proprio dono e o oposto do que o board existe para
        // fazer — e o risco aparece quando o animal chega desacordado num plantao que
        // nao e o de sempre
        Assert.Equal("RN-068", ex.Codigo);
        Assert.False(registro.Oculto);
    }

    [Fact]
    public async Task ExibirDeNovo_RegistroComAlerta_ESempreAceito()
    {
        var (animal, registro) = CenarioDeHistorico("Reacao a Dipirona durante o pos-operatorio.");
        animal.DefinirPerfilClinico(alergias: ["Dipirona"]);
        registro.DefinirVisibilidade(true);

        var resultado = await CriarServico().DefinirVisibilidadeDoHistoricoAsync(
            animal.Id, registro.Id, oculto: false);

        // A guarda so vale numa direcao: voltar a exibir nunca esconde nada
        Assert.False(resultado.Oculto);
    }

    [Fact]
    public async Task OcultarDoHistorico_PorVeterinario_LancaAcessoNegadoRN105()
    {
        var animal = CriarAnimalDe(Guid.NewGuid());
        var registro = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta de rotina.");

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ObterHistoricoLongitudinalAsync(animal.Id)).ReturnsAsync([registro]);
        ComoVeterinario(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().DefinirVisibilidadeDoHistoricoAsync(animal.Id, registro.Id, oculto: true));

        // O veterinario nao decide o que o Responsavel enxerga sobre o proprio animal
        Assert.Equal("RN-105", ex.Codigo);
    }

    // ── RN-046: o board nasce preenchido ───────────────────────────────────

    [Fact]
    public async Task CriarAsync_DerivaAsObrigacoesDaCarteira()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Animal>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().CriarAsync(CriarDto());

        // Cadastrar o pet e informar a carteira e, para o Responsavel, a mesma acao:
        // deixar as obrigacoes para uma chamada separada faria o board aparecer vazio
        // logo depois de ele ter digitado exatamente os dados que o preenchem
        _obrigacoesMock.Verify(o => o.DerivarDaCarteiraAsync(resultado.Id), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_QuandoADerivacaoFalha_OAnimalContinuaCadastrado()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Animal>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _obrigacoesMock.Setup(o => o.DerivarDaCarteiraAsync(It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("board indisponivel"));

        var resultado = await CriarServico().CriarAsync(CriarDto());

        // Perder o pet recem-cadastrado por causa do board seria trocar o essencial
        // pelo acessorio
        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal("Thor", resultado.Nome);
    }
}
