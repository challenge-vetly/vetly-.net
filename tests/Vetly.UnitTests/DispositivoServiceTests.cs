using Moq;
using Vetly.Application.DTOs.Dispositivo;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes do registro de dispositivos para push (RN-007/RN-092).
/// </summary>
public class DispositivoServiceTests
{
    private readonly Mock<IDispositivoRepository> _repo = new();
    private readonly Mock<ITutorRepository> _tutorRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();

    public DispositivoServiceTests()
    {
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);
        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _tutorRepo.Setup(r => r.ObterPorIdAsync(_tutorId))
            .ReturnsAsync(new Tutor("Ana", "ana@exemplo.com", "11999998888"));
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<Dispositivo>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.Atualizar(It.IsAny<Dispositivo>()));
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
    }

    private DispositivoService CriarServico() =>
        new(_repo.Object, _tutorRepo.Object, _usuario.Object);

    private static RegistrarDispositivoDto Dto(string token = "token-de-push-do-fabricante") => new()
    {
        PushToken = token,
        Plataforma = PlataformaDispositivo.Android
    };

    [Fact]
    public async Task RegistrarAsync_DispositivoNovo_Persiste()
    {
        _repo.Setup(r => r.ObterPorPushTokenAsync(It.IsAny<string>())).ReturnsAsync((Dispositivo?)null);

        var resultado = await CriarServico().RegistrarAsync(_tutorId, Dto());

        Assert.Equal(_tutorId, resultado.TutorId);
        Assert.True(resultado.Ativo);
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<Dispositivo>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarAsync_MesmoTokenDoMesmoTutor_ReaproveitaOCadastro()
    {
        var existente = new Dispositivo(_tutorId, "token-de-push-do-fabricante", PlataformaDispositivo.Android);
        existente.Desativar();
        _repo.Setup(r => r.ObterPorPushTokenAsync("token-de-push-do-fabricante")).ReturnsAsync(existente);

        var resultado = await CriarServico().RegistrarAsync(_tutorId, Dto());

        // Reinstalar o app nao pode criar registro duplicado
        Assert.Equal(existente.Id, resultado.Id);
        Assert.True(existente.Ativo);
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<Dispositivo>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAsync_TokenQueEraDeOutroTutor_DesativaORegistroAntigo()
    {
        var deOutro = new Dispositivo(Guid.NewGuid(), "token-de-push-do-fabricante", PlataformaDispositivo.Ios);
        _repo.Setup(r => r.ObterPorPushTokenAsync("token-de-push-do-fabricante")).ReturnsAsync(deOutro);

        var resultado = await CriarServico().RegistrarAsync(_tutorId, Dto());

        // O aparelho trocou de dono: o push do dono anterior nao pode cair nele
        Assert.False(deOutro.Ativo);
        Assert.Equal(_tutorId, resultado.TutorId);
        _repo.Verify(r => r.AdicionarAsync(It.IsAny<Dispositivo>()), Times.Once);
    }

    [Fact]
    public async Task RegistrarAsync_ParaOutroResponsavel_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarAsync(Guid.NewGuid(), Dto()));

        Assert.Equal("RN-105", ex.Codigo);
    }

    [Fact]
    public async Task RegistrarAsync_NaoDevolveOPushTokenInteiro()
    {
        _repo.Setup(r => r.ObterPorPushTokenAsync(It.IsAny<string>())).ReturnsAsync((Dispositivo?)null);

        var resultado = await CriarServico().RegistrarAsync(_tutorId, Dto("token-secreto-do-aparelho"));

        Assert.DoesNotContain("token-secreto", resultado.PushToken);
        Assert.StartsWith("***", resultado.PushToken);
    }

    [Fact]
    public async Task RemoverAsync_DispositivoDeOutroTutor_LancaAcessoNegado()
    {
        var deOutro = new Dispositivo(Guid.NewGuid(), "token-alheio", PlataformaDispositivo.Ios);
        _repo.Setup(r => r.ObterPorIdAsync(deOutro.Id)).ReturnsAsync(deOutro);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RemoverAsync(_tutorId, deOutro.Id));

        Assert.Equal("RN-105", ex.Codigo);
        Assert.True(deOutro.Ativo);
    }

    [Fact]
    public async Task RemoverAsync_DispositivoProprio_DesativaSemApagar()
    {
        var meu = new Dispositivo(_tutorId, "meu-token", PlataformaDispositivo.Android);
        _repo.Setup(r => r.ObterPorIdAsync(meu.Id)).ReturnsAsync(meu);

        await CriarServico().RemoverAsync(_tutorId, meu.Id);

        // Remocao logica: o historico de entrega de push depende do registro
        Assert.False(meu.Ativo);
        _repo.Verify(r => r.Remover(It.IsAny<Dispositivo>()), Times.Never);
    }

    [Fact]
    public async Task ObterDoTutorAsync_DeOutroResponsavel_LancaAcessoNegado()
    {
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterDoTutorAsync(Guid.NewGuid()));

        Assert.Equal("RN-105", ex.Codigo);
    }
}
