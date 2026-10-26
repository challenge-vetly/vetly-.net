using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Serviço do avatar (RN-112 a RN-114): a mesma guarda de acesso do board e a
/// montagem dos dados que o cálculo precisa.
/// </summary>
public class AvatarServiceTests
{
    private readonly Mock<IAnimalRepository> _animais = new();
    private readonly Mock<IColmeiaService> _colmeia = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();
    private readonly Mock<IObrigacaoRepository> _obrigacoes = new();
    private readonly Mock<IConsultaRepository> _consultas = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly Animal _animal;

    public AvatarServiceTests()
    {
        _animal = new Animal("Thor", "Canino", "SRD", new DateTime(2022, 3, 1), _tutorId);
        _animal.RegistrarPeso(28m);

        _animais.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(_animal);
        _obrigacoes.Setup(r => r.ObterDoAnimalAsync(_animal.Id, true)).ReturnsAsync([]);
        _consultas.Setup(r => r.ObterDataDaUltimaRealizadaAsync(_animal.Id)).ReturnsAsync(DateTime.UtcNow.AddMonths(-1));
    }

    private AvatarService CriarServico() =>
        new(_animais.Object, _colmeia.Object, _usuario.Object,
            new CalculadoraDoAvatar(_obrigacoes.Object, _consultas.Object));

    private void ComoResponsavel(Guid tutorId)
    {
        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(tutorId);
    }

    [Fact]
    public async Task Obter_DonoDoAnimal_DevolveACondicaoCalculada()
    {
        // Arrange
        ComoResponsavel(_tutorId);

        // Act
        var avatar = await CriarServico().ObterAsync(_animal.Id);

        // Assert
        Assert.Equal(_animal.Id, avatar.AnimalId);
        Assert.Equal(100, avatar.Saude);
        Assert.Equal(EstadoDoAvatar.Feliz, avatar.Estado);
        Assert.True(avatar.SpriteDisponivel);
    }

    [Fact]
    public async Task Obter_LeAsObrigacoesInclusiveArquivadas()
    {
        // Arrange — o bonus de humor conta retorno cumprido no prazo, que se arquiva
        ComoResponsavel(_tutorId);

        // Act
        await CriarServico().ObterAsync(_animal.Id);

        // Assert
        _obrigacoes.Verify(r => r.ObterDoAnimalAsync(_animal.Id, true), Times.Once);
    }

    [Fact]
    public async Task Obter_AnimalDeOutroResponsavel_LancaAcessoNegado()
    {
        // Arrange
        ComoResponsavel(Guid.NewGuid());

        // Act / Assert
        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().ObterAsync(_animal.Id));
    }

    [Fact]
    public async Task Obter_VeterinarioQueAtende_Alcanca()
    {
        // Arrange
        var vetId = Guid.NewGuid();
        _usuario.SetupGet(u => u.EhVeterinario).Returns(true);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(vetId);
        _animais.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, _animal.Id)).ReturnsAsync(true);

        // Act
        var avatar = await CriarServico().ObterAsync(_animal.Id);

        // Assert
        Assert.Equal(_animal.Id, avatar.AnimalId);
    }

    [Fact]
    public async Task Obter_VeterinarioDeForaSemColmeia_LancaERegistraATentativa()
    {
        // Arrange — mesma regra do board: tentativa negada tambem vai para a trilha
        var vetId = Guid.NewGuid();
        _usuario.SetupGet(u => u.EhVeterinario).Returns(true);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(vetId);
        _colmeia.Setup(c => c.PodeAcessarAsync(vetId, _animal.Id, EscopoAcessoColmeia.HistoricoCompleto))
            .ReturnsAsync(false);

        // Act
        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().ObterAsync(_animal.Id));

        // Assert
        _colmeia.Verify(c => c.RegistrarAcessoAsync(
            _animal.Id, EscopoAcessoColmeia.HistoricoCompleto, false, nameof(AvatarService)), Times.Once);
    }

    [Fact]
    public async Task Obter_AnimalInexistente_LancaNotFound()
    {
        // Arrange
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        // Act / Assert
        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().ObterAsync(Guid.NewGuid()));
    }

    [Theory]
    [InlineData("Canino", true)]
    [InlineData("cachorro", true)]
    [InlineData("  Cão ", true)]
    [InlineData("Felino", false)]
    [InlineData("Gato", false)]
    [InlineData("", false)]
    public async Task Obter_SpriteSoParaCachorro(string especie, bool esperado)
    {
        // Arrange — especie e texto livre no cadastro
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);
        var animal = new Animal("Pet", especie, "SRD", new DateTime(2022, 3, 1), _tutorId);
        _animais.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _obrigacoes.Setup(r => r.ObterDoAnimalAsync(animal.Id, true)).ReturnsAsync([]);

        // Act
        var avatar = await CriarServico().ObterAsync(animal.Id);

        // Assert
        Assert.Equal(esperado, avatar.SpriteDisponivel);
    }

    [Fact]
    public async Task Obter_ObrigacaoVencida_DerrubaASaudeEExplicaOMotivo()
    {
        // Arrange
        ComoResponsavel(_tutorId);
        _obrigacoes.Setup(r => r.ObterDoAnimalAsync(_animal.Id, true)).ReturnsAsync(
        [
            new ObrigacaoPet(_animal.Id, _tutorId, TipoObrigacaoPet.Vacina, "V10", DateTime.UtcNow.AddDays(-3), 365)
        ]);

        // Act
        var avatar = await CriarServico().ObterAsync(_animal.Id);

        // Assert
        Assert.Equal(60, avatar.Saude);
        Assert.Equal(EstadoDoAvatar.VacinaAtrasada, avatar.Estado);
        Assert.Equal([MotivoDoAvatar.VacinaVencida], avatar.Motivos);
        Assert.Single(avatar.Falas);
    }
}
