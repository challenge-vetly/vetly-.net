using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do AnimalService.
/// Cobre atualizacao de peso, ocultacao de prontuarios (RN-088) e a colmeia por evento
/// clinico ao listar o historico (RN-010/083 — acesso completo x restrito x negado).
/// </summary>
public class AnimalServiceTests
{
    private readonly Mock<IAnimalRepository> _repoMock = new();
    private readonly Mock<IRegistroOcultadoRepository> _registroOcultadoRepoMock = new();
    private readonly Mock<IAcessoProntuarioService> _acessoProntuarioServiceMock = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    private AnimalService CriarServico() =>
        new(_repoMock.Object, _registroOcultadoRepoMock.Object, _acessoProntuarioServiceMock.Object,
            _currentUserMock.Object, TimeProvider.System);

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
    public async Task ObterHistoricoAsync_VeterinarioComAcessoCompleto_FiltraProntuarioOcultado()
    {
        var animal = CriarAnimal();
        var vetId = Guid.NewGuid();
        var prontuarioVisivel = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta 1");
        var prontuarioOcultado = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta 2 - dado sensivel");

        _currentUserMock.Setup(c => c.Role).Returns("Veterinario");
        _currentUserMock.Setup(c => c.EntidadeId).Returns(vetId);
        _acessoProntuarioServiceMock.Setup(s => s.PodeAcessarAsync(vetId, animal.Id, It.IsAny<DateTime>())).ReturnsAsync(true);
        _acessoProntuarioServiceMock.Setup(s => s.TemAcessoCompletoAsync(vetId, animal.Id, It.IsAny<DateTime>())).ReturnsAsync(true);
        _repoMock.Setup(r => r.ObterHistoricoLongitudinalAsync(animal.Id))
            .ReturnsAsync([prontuarioVisivel, prontuarioOcultado]);
        _registroOcultadoRepoMock.Setup(r => r.ObterPorAnimalAsync(animal.Id))
            .ReturnsAsync([new RegistroOcultado(animal.Id, prontuarioOcultado.Id, DateTime.UtcNow)]);

        var resultado = (await CriarServico().ObterHistoricoAsync(animal.Id)).ToList();

        Assert.Single(resultado);
        Assert.Equal(prontuarioVisivel.Id, resultado[0].Id);
        _acessoProntuarioServiceMock.Verify(
            s => s.RegistrarAcessoAsync(vetId, animal.Id, It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ObterHistoricoAsync_VeterinarioComAcessoRestrito_VeSoOQueProduziu()
    {
        var animal = CriarAnimal();
        var vetId = Guid.NewGuid();
        var prontuarioProprio = new Prontuario(Guid.NewGuid(), animal.Id, "Consulta que este vet fez");

        _currentUserMock.Setup(c => c.Role).Returns("Veterinario");
        _currentUserMock.Setup(c => c.EntidadeId).Returns(vetId);
        _acessoProntuarioServiceMock.Setup(s => s.PodeAcessarAsync(vetId, animal.Id, It.IsAny<DateTime>())).ReturnsAsync(true);
        _acessoProntuarioServiceMock.Setup(s => s.TemAcessoCompletoAsync(vetId, animal.Id, It.IsAny<DateTime>())).ReturnsAsync(false);
        _repoMock.Setup(r => r.ObterHistoricoLongitudinalPorVeterinarioAsync(animal.Id, vetId))
            .ReturnsAsync([prontuarioProprio]);
        _registroOcultadoRepoMock.Setup(r => r.ObterPorAnimalAsync(animal.Id)).ReturnsAsync([]);

        var resultado = (await CriarServico().ObterHistoricoAsync(animal.Id)).ToList();

        Assert.Single(resultado);
        Assert.Equal(prontuarioProprio.Id, resultado[0].Id);
        _repoMock.Verify(r => r.ObterHistoricoLongitudinalAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ObterHistoricoAsync_VeterinarioSemNenhumAcesso_LancaForbiddenExceptionACESSO001()
    {
        var animal = CriarAnimal();
        var vetId = Guid.NewGuid();

        _currentUserMock.Setup(c => c.Role).Returns("Veterinario");
        _currentUserMock.Setup(c => c.EntidadeId).Returns(vetId);
        _acessoProntuarioServiceMock.Setup(s => s.PodeAcessarAsync(vetId, animal.Id, It.IsAny<DateTime>())).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => CriarServico().ObterHistoricoAsync(animal.Id));

        Assert.Equal("ACESSO-001", ex.Codigo);
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
        // Papel Responsavel nunca precisa consultar quais prontuarios estao ocultados nem a colmeia.
        _registroOcultadoRepoMock.Verify(r => r.ObterPorAnimalAsync(It.IsAny<Guid>()), Times.Never);
        _acessoProntuarioServiceMock.Verify(
            s => s.PodeAcessarAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }
}
