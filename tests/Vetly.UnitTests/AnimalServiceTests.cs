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

    private AnimalService CriarServico() => new(_repoMock.Object);

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
}
