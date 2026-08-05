using Moq;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do AcessoProntuarioService.
/// Cobre a colmeia por evento clinico (RN-083), o acesso restrito classico (RN-010)
/// e o registro de log de acesso (RN-086).
/// </summary>
public class AcessoProntuarioServiceTests
{
    private readonly Mock<IConcessaoAcessoProntuarioRepository> _concessaoRepoMock = new();
    private readonly Mock<ILogAcessoProntuarioRepository> _logRepoMock = new();
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private readonly Mock<IConsentimentoLgpdRepository> _consentimentoRepoMock = new();

    private AcessoProntuarioService CriarServico() => new(
        _concessaoRepoMock.Object, _logRepoMock.Object, _consultaRepoMock.Object, _consentimentoRepoMock.Object);

    [Fact]
    public async Task PodeAcessarAsync_ComConcessaoAtiva_RetornaTrue()
    {
        var vetId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        var concessao = new ConcessaoAcessoProntuario(animalId, vetId, Guid.NewGuid(), BaseAcesso.ConsentimentoRede, agora, agora.AddHours(24));

        _concessaoRepoMock.Setup(r => r.ObterAtivaAsync(vetId, animalId, agora)).ReturnsAsync(concessao);

        Assert.True(await CriarServico().PodeAcessarAsync(vetId, animalId, agora));
    }

    [Fact]
    public async Task PodeAcessarAsync_SemConcessaoMasComAtendimentoDireto_RetornaTrue()
    {
        var vetId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        _concessaoRepoMock.Setup(r => r.ObterAtivaAsync(vetId, animalId, agora)).ReturnsAsync((ConcessaoAcessoProntuario?)null);
        _consultaRepoMock.Setup(r => r.ExisteConsultaAsync(vetId, animalId)).ReturnsAsync(true);

        Assert.True(await CriarServico().PodeAcessarAsync(vetId, animalId, agora));
    }

    [Fact]
    public async Task PodeAcessarAsync_SemConcessaoESemAtendimento_RetornaFalse()
    {
        var vetId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        _concessaoRepoMock.Setup(r => r.ObterAtivaAsync(vetId, animalId, agora)).ReturnsAsync((ConcessaoAcessoProntuario?)null);
        _consultaRepoMock.Setup(r => r.ExisteConsultaAsync(vetId, animalId)).ReturnsAsync(false);

        Assert.False(await CriarServico().PodeAcessarAsync(vetId, animalId, agora));
    }

    [Fact]
    public async Task ConcederAcessoPorConsultaAsync_ComConsentimentoDeRedeAtivo_CriaConcessao()
    {
        var responsavelId = Guid.NewGuid();
        var dataConsulta = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var agora = DateTime.UtcNow;

        _consentimentoRepoMock
            .Setup(r => r.ObterAtivoAsync(responsavelId, FinalidadeConsentimento.CompartilhamentoRede))
            .ReturnsAsync(new ConsentimentoLgpd(responsavelId, FinalidadeConsentimento.CompartilhamentoRede, agora));
        _concessaoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<ConcessaoAcessoProntuario>())).Returns(Task.CompletedTask);
        _concessaoRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        await CriarServico().ConcederAcessoPorConsultaAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), responsavelId, dataConsulta, agora);

        _concessaoRepoMock.Verify(r => r.AdicionarAsync(It.Is<ConcessaoAcessoProntuario>(
            c => c.ExpiraEm == dataConsulta.AddHours(24))), Times.Once);
    }

    [Fact]
    public async Task ConcederAcessoPorConsultaAsync_SemConsentimentoDeRede_NaoCriaConcessao()
    {
        var responsavelId = Guid.NewGuid();
        _consentimentoRepoMock
            .Setup(r => r.ObterAtivoAsync(responsavelId, FinalidadeConsentimento.CompartilhamentoRede))
            .ReturnsAsync((ConsentimentoLgpd?)null);

        await CriarServico().ConcederAcessoPorConsultaAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), responsavelId, DateTime.UtcNow, DateTime.UtcNow);

        _concessaoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<ConcessaoAcessoProntuario>()), Times.Never);
    }

    [Fact]
    public async Task RegistrarAcessoAsync_ComAcessoCompleto_GravaLogComBaseConsentimentoRede()
    {
        var vetId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        var concessao = new ConcessaoAcessoProntuario(animalId, vetId, Guid.NewGuid(), BaseAcesso.ConsentimentoRede, agora, agora.AddHours(24));

        _concessaoRepoMock.Setup(r => r.ObterAtivaAsync(vetId, animalId, agora)).ReturnsAsync(concessao);
        _logRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<LogAcessoProntuario>())).Returns(Task.CompletedTask);
        _logRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        await CriarServico().RegistrarAcessoAsync(vetId, animalId, "Briefing pré-consulta", agora);

        _logRepoMock.Verify(r => r.AdicionarAsync(It.Is<LogAcessoProntuario>(
            l => l.BaseAcesso == BaseAcesso.ConsentimentoRede)), Times.Once);
    }

    [Fact]
    public async Task RegistrarAcessoAsync_SemAcessoCompleto_GravaLogComBaseAtendimentoDireto()
    {
        var vetId = Guid.NewGuid();
        var animalId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        _concessaoRepoMock.Setup(r => r.ObterAtivaAsync(vetId, animalId, agora)).ReturnsAsync((ConcessaoAcessoProntuario?)null);
        _logRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<LogAcessoProntuario>())).Returns(Task.CompletedTask);
        _logRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        await CriarServico().RegistrarAcessoAsync(vetId, animalId, "Listagem de prontuários", agora);

        _logRepoMock.Verify(r => r.AdicionarAsync(It.Is<LogAcessoProntuario>(
            l => l.BaseAcesso == BaseAcesso.AtendimentoDireto)), Times.Once);
    }
}
