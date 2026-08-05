using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do AnimalService.
/// Cobre atualizacao de peso, ocultacao de prontuarios (RN-088) e o filtro por papel do chamador.
/// </summary>
public class AnimalServiceTests
{
    private readonly Mock<IAnimalRepository> _repoMock = new();
    private readonly Mock<IRegistroOcultadoRepository> _registroOcultadoRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    private AnimalService CriarServico() =>
        new(_repoMock.Object, _registroOcultadoRepoMock.Object, _currentUserMock.Object, TimeProvider.System);

    private static Animal CriarAnimal() =>
        new("Rex", "Canino", "Labrador", SexoAnimal.Macho, new DateTime(2020, 1, 1), Guid.NewGuid());

    [Fact]
    public async Task AtualizarPesoAsync_PesoValido_PersisteEDevolveDto()
    {
        var animal = CriarAnimal();
        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Animal>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().AtualizarPesoAsync(animal.Id, 15.5m);

        Assert.Equal(15.5m, resultado.PesoKg);
        _repoMock.Verify(r => r.SalvarAsync(), Times.Once);
    }

    [Fact]
    public async Task OcultarRegistroAsync_ProntuarioClassificadoComoAlertaSeguranca_LancaDomainExceptionANIMAL002()
    {
        var animal = CriarAnimal();
        var prontuario = new Prontuario(Guid.NewGuid(), animal.Id, "Alergia a penicilina", alertaSeguranca: true);

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ObterProntuarioPorIdAsync(prontuario.Id)).ReturnsAsync(prontuario);

        await Assert.ThrowsAsync<Vetly.Domain.Exceptions.DomainException>(
            () => CriarServico().OcultarRegistroAsync(animal.Id, prontuario.Id));

        _registroOcultadoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroOcultado>()), Times.Never);
    }

    [Fact]
    public async Task OcultarRegistroAsync_ProntuarioComum_CriaRegistroOcultado()
    {
        var animal = CriarAnimal();
        var prontuario = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta de rotina");

        _repoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ObterProntuarioPorIdAsync(prontuario.Id)).ReturnsAsync(prontuario);
        _registroOcultadoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<RegistroOcultado>())).Returns(Task.CompletedTask);
        _registroOcultadoRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        await CriarServico().OcultarRegistroAsync(animal.Id, prontuario.Id);

        _registroOcultadoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<RegistroOcultado>()), Times.Once);
    }

    [Fact]
    public async Task ObterHistoricoAsync_ChamadorVeterinario_FiltraProntuarioOcultado()
    {
        var animal = CriarAnimal();
        var prontuarioVisivel = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta 1");
        var prontuarioOcultado = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta 2 - dado sensivel");

        _repoMock.Setup(r => r.ObterHistoricoLongitudinalAsync(animal.Id))
            .ReturnsAsync([prontuarioVisivel, prontuarioOcultado]);
        _registroOcultadoRepoMock.Setup(r => r.ObterPorAnimalAsync(animal.Id))
            .ReturnsAsync([new RegistroOcultado(animal.Id, prontuarioOcultado.Id, DateTime.UtcNow)]);
        _currentUserMock.Setup(c => c.Role).Returns("Veterinario");

        var resultado = (await CriarServico().ObterHistoricoAsync(animal.Id)).ToList();

        Assert.Single(resultado);
        Assert.Equal(prontuarioVisivel.Id, resultado[0].Id);
    }

    [Fact]
    public async Task ObterHistoricoAsync_ChamadorResponsavel_VeTodosOsProntuariosInclusiveOcultados()
    {
        var animal = CriarAnimal();
        var prontuarioVisivel = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta 1");
        var prontuarioOcultado = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta 2 - dado sensivel");

        _repoMock.Setup(r => r.ObterHistoricoLongitudinalAsync(animal.Id))
            .ReturnsAsync([prontuarioVisivel, prontuarioOcultado]);
        _currentUserMock.Setup(c => c.Role).Returns("Responsavel");

        var resultado = (await CriarServico().ObterHistoricoAsync(animal.Id)).ToList();

        Assert.Equal(2, resultado.Count);
        // Papel Responsavel nunca precisa consultar quais prontuarios estao ocultados.
        _registroOcultadoRepoMock.Verify(r => r.ObterPorAnimalAsync(It.IsAny<Guid>()), Times.Never);
    }
}
