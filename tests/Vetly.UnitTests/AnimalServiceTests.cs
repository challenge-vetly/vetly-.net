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
}
