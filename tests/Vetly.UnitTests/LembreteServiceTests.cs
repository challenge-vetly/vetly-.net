using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do LembreteService.
/// Cobre agendamento, alerta apos 3 tentativas (RN-095) e encerramento da regua (RN-094).
/// </summary>
public class LembreteServiceTests
{
    private readonly Mock<ILembreteRepository> _repoMock = new();

    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private readonly Mock<IUsuarioAtual> _usuarioMock = new();

    /// <summary>Por padrao os testes rodam como Admin, que alcanca todo o escopo.</summary>
    public LembreteServiceTests() => _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

    private LembreteService CriarServico() =>
        new(_repoMock.Object, _animalRepoMock.Object, _usuarioMock.Object);

    [Fact]
    public async Task AgendarLembreteAsync_CriaEPersisteLembrete()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<LembreteAgendado>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().AgendarLembreteAsync(
            Guid.NewGuid(), Guid.NewGuid(), TipoLembrete.Vacina, DateTime.UtcNow.AddDays(30));

        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal(TipoLembrete.Vacina, resultado.Tipo);
        Assert.False(resultado.TutorRespondeu);
    }

    [Fact]
    public async Task ProcessarTentativaAsync_Apos3Tentativas_AlertaClinica()
    {
        var lembrete = new LembreteAgendado(Guid.NewGuid(), Guid.NewGuid(), TipoLembrete.Retorno, DateTime.UtcNow);
        lembrete.RegistrarTentativa();
        lembrete.RegistrarTentativa();

        _repoMock.Setup(r => r.ObterPorIdAsync(lembrete.Id)).ReturnsAsync(lembrete);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<LembreteAgendado>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().ProcessarTentativaAsync(lembrete.Id);

        Assert.Equal(3, resultado.TentativasRealizadas);
        Assert.True(resultado.AlertaEnviadoClinica);
    }

    [Fact]
    public async Task RegistrarRespostaAsync_EncerraDeguaDeContato()
    {
        var lembrete = new LembreteAgendado(Guid.NewGuid(), Guid.NewGuid(), TipoLembrete.Vacina, DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorIdAsync(lembrete.Id)).ReturnsAsync(lembrete);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<LembreteAgendado>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().RegistrarRespostaAsync(lembrete.Id);

        Assert.True(resultado.TutorRespondeu);

        // Tentativa apos resposta deve lancar LEMBRETE-001
        _repoMock.Setup(r => r.ObterPorIdAsync(lembrete.Id)).ReturnsAsync(resultado);
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ProcessarTentativaAsync(lembrete.Id));
        Assert.Equal("LEMBRETE-001", ex.Codigo);
    }

    // ── RN-105/RN-106: quem cria a regua e quem a encerra ───────────────────

    private void ComoVeterinario(Guid veterinarioId)
    {
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhVeterinario).Returns(true);
        _usuarioMock.SetupGet(u => u.VeterinarioId).Returns(veterinarioId);
    }

    private void ComoTutor(Guid tutorId)
    {
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhTutor).Returns(true);
        _usuarioMock.SetupGet(u => u.TutorId).Returns(tutorId);
    }

    [Fact]
    public async Task AgendarLembreteAsync_VeterinarioQueNaoAtendeOAnimal_LancaAcessoNegadoRN105()
    {
        var vetId = Guid.NewGuid();
        var animalId = Guid.NewGuid();

        _animalRepoMock.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, animalId)).ReturnsAsync(false);
        ComoVeterinario(vetId);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().AgendarLembreteAsync(
                animalId, Guid.NewGuid(), TipoLembrete.Vacina, DateTime.UtcNow.AddDays(30)));

        // A regua termina em push no telefone do Responsavel: quem nao atende o
        // animal nao dispara contato sobre ele
        Assert.Equal("RN-105", ex.Codigo);
        _repoMock.Verify(r => r.AdicionarAsync(It.IsAny<LembreteAgendado>()), Times.Never);
    }

    [Fact]
    public async Task AgendarLembreteAsync_VeterinarioQueAtendeOAnimal_Agenda()
    {
        var vetId = Guid.NewGuid();
        var animalId = Guid.NewGuid();

        _animalRepoMock.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, animalId)).ReturnsAsync(true);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<LembreteAgendado>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        ComoVeterinario(vetId);

        var resultado = await CriarServico().AgendarLembreteAsync(
            animalId, Guid.NewGuid(), TipoLembrete.Vacina, DateTime.UtcNow.AddDays(30));

        Assert.Equal(animalId, resultado.AnimalId);
    }

    [Fact]
    public async Task ProcessarTentativaAsync_VeterinarioDeFora_LancaAcessoNegadoRN105()
    {
        var vetId = Guid.NewGuid();
        var lembrete = new LembreteAgendado(Guid.NewGuid(), Guid.NewGuid(), TipoLembrete.Retorno, DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorIdAsync(lembrete.Id)).ReturnsAsync(lembrete);
        _animalRepoMock.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, lembrete.AnimalId)).ReturnsAsync(false);
        ComoVeterinario(vetId);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ProcessarTentativaAsync(lembrete.Id));

        Assert.Equal("RN-105", ex.Codigo);
        Assert.Equal(0, lembrete.TentativasRealizadas);
    }

    [Fact]
    public async Task RegistrarRespostaAsync_TutorDeOutroLembrete_LancaAcessoNegadoRN106()
    {
        var lembrete = new LembreteAgendado(Guid.NewGuid(), Guid.NewGuid(), TipoLembrete.Retorno, DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorIdAsync(lembrete.Id)).ReturnsAsync(lembrete);
        ComoTutor(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarRespostaAsync(lembrete.Id));

        Assert.Equal("RN-106", ex.Codigo);
        Assert.False(lembrete.TutorRespondeu);
    }

    [Fact]
    public async Task RegistrarRespostaAsync_TutorDono_EncerraARegua()
    {
        var tutorId = Guid.NewGuid();
        var lembrete = new LembreteAgendado(Guid.NewGuid(), tutorId, TipoLembrete.Retorno, DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorIdAsync(lembrete.Id)).ReturnsAsync(lembrete);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<LembreteAgendado>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        ComoTutor(tutorId);

        var resultado = await CriarServico().RegistrarRespostaAsync(lembrete.Id);

        // RN-094: quem encerra a regua e quem diz que recebeu o recado
        Assert.True(resultado.TutorRespondeu);
    }
}
