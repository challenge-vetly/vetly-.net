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

    private LembreteService CriarServico() => new(_repoMock.Object);

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
}
