using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Factories;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do ObrigacaoService.
/// Cobre a selecao de IObrigacaoFactory por especie (RN-069) e a trava contra gerar o
/// calendario duas vezes para o mesmo animal (OBRIGACAO-002).
/// </summary>
public class ObrigacaoServiceTests
{
    private readonly Mock<IObrigacaoDoPetRepository> _repoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private static readonly IObrigacaoFactory[] TodasAsFactories =
        [new ObrigacaoCaninaFactory(), new ObrigacaoFelinaFactory(), new ObrigacaoGenericaFactory()];
    private readonly FakeTimeProvider _timeProvider = new(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    private ObrigacaoService CriarServico() =>
        new(_repoMock.Object, _animalRepoMock.Object, TodasAsFactories, _timeProvider);

    private static Animal CriarAnimal(string especie = "Canino") =>
        new("Rex", especie, "SRD", SexoAnimal.Macho, new DateTime(2020, 1, 1), Guid.NewGuid());

    [Fact]
    public async Task GerarCalendarioAsync_AnimalNaoEncontrado_LancaNotFoundException()
    {
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync((Animal?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().GerarCalendarioAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GerarCalendarioAsync_CalendarioJaExiste_LancaBusinessRuleExceptionOBRIGACAO002()
    {
        var animal = CriarAnimal();
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ExisteCalendarioAsync(animal.Id)).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().GerarCalendarioAsync(animal.Id));

        Assert.Equal("OBRIGACAO-002", ex.Codigo);
    }

    [Fact]
    public async Task GerarCalendarioAsync_AnimalCanino_SelecionaFactoryCaninaEPersisteQuatroObrigacoes()
    {
        var animal = CriarAnimal("Canino");
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ExisteCalendarioAsync(animal.Id)).ReturnsAsync(false);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<ObrigacaoDoPet>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = (await CriarServico().GerarCalendarioAsync(animal.Id)).ToList();

        Assert.Equal(4, resultado.Count);
        _repoMock.Verify(r => r.AdicionarAsync(It.IsAny<ObrigacaoDoPet>()), Times.Exactly(4));
        _repoMock.Verify(r => r.SalvarAsync(), Times.Once);
    }

    [Fact]
    public async Task GerarCalendarioAsync_EspecieSemFactoryDedicada_UsaFallbackGenerico()
    {
        var animal = CriarAnimal("Ave");
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ExisteCalendarioAsync(animal.Id)).ReturnsAsync(false);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<ObrigacaoDoPet>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = (await CriarServico().GerarCalendarioAsync(animal.Id)).ToList();

        Assert.Equal(2, resultado.Count); // calendario generico tem menos eventos
    }

    [Fact]
    public async Task ObterPorAnimalAsync_ObrigacaoPendenteAposDataLimite_MarcaAtrasadaTrue()
    {
        var animal = CriarAnimal();
        var obrigacaoAtrasada = new ObrigacaoDoPet(animal.Id, TipoObrigacao.Vacina, _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1));

        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ObterPorAnimalAsync(animal.Id)).ReturnsAsync([obrigacaoAtrasada]);

        var resultado = (await CriarServico().ObterPorAnimalAsync(animal.Id)).ToList();

        Assert.Single(resultado);
        Assert.True(resultado[0].Atrasada);
    }
}
