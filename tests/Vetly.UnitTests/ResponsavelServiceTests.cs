using Moq;
using Vetly.Application.DTOs.Responsavel;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do ResponsavelService.
/// Cobre concessao/revogacao de consentimento LGPD granular com preservacao de historico (RN-041..044).
/// </summary>
public class ResponsavelServiceTests
{
    private readonly Mock<IResponsavelRepository> _repoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private readonly Mock<IConsentimentoLgpdRepository> _consentimentoRepoMock = new();

    private ResponsavelService CriarServico() =>
        new(_repoMock.Object, _animalRepoMock.Object, _consentimentoRepoMock.Object, TimeProvider.System);

    private static Responsavel CriarResponsavel() =>
        new("Responsavel Teste", "responsavel@teste.com", "11999999999");

    [Fact]
    public async Task ConcederConsentimentoAsync_ResponsavelExistente_AdicionaNovoConsentimentoAtivo()
    {
        var responsavel = CriarResponsavel();
        _repoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _consentimentoRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<ConsentimentoLgpd>())).Returns(Task.CompletedTask);
        _consentimentoRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = new ConcederConsentimentoDto { Finalidade = FinalidadeConsentimento.CompartilhamentoRede };
        var resultado = await CriarServico().ConcederConsentimentoAsync(responsavel.Id, dto);

        Assert.True(resultado.Ativo);
        Assert.Equal(FinalidadeConsentimento.CompartilhamentoRede, resultado.Finalidade);
        _consentimentoRepoMock.Verify(r => r.AdicionarAsync(It.IsAny<ConsentimentoLgpd>()), Times.Once);
    }

    [Fact]
    public async Task ConcederConsentimentoAsync_ResponsavelInexistente_LancaNotFoundException()
    {
        var id = Guid.NewGuid();
        _repoMock.Setup(r => r.ObterPorIdAsync(id)).ReturnsAsync((Responsavel?)null);

        var dto = new ConcederConsentimentoDto { Finalidade = FinalidadeConsentimento.Promocoes };
        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().ConcederConsentimentoAsync(id, dto));
    }

    [Fact]
    public async Task RevogarConsentimentoAsync_ConsentimentoAtivoExiste_RevogaSemApagarORegistro()
    {
        var responsavel = CriarResponsavel();
        var consentimento = new ConsentimentoLgpd(responsavel.Id, FinalidadeConsentimento.Promocoes, DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _consentimentoRepoMock
            .Setup(r => r.ObterAtivoAsync(responsavel.Id, FinalidadeConsentimento.Promocoes))
            .ReturnsAsync(consentimento);
        _consentimentoRepoMock.Setup(r => r.Atualizar(It.IsAny<ConsentimentoLgpd>()));
        _consentimentoRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().RevogarConsentimentoAsync(responsavel.Id, FinalidadeConsentimento.Promocoes);

        Assert.False(resultado.Ativo);
        Assert.NotNull(resultado.DataRevogacao);
        // Revogar so muta o registro existente — nunca chama Remover (historico preservado, RN-044/087).
        _consentimentoRepoMock.Verify(r => r.Remover(It.IsAny<ConsentimentoLgpd>()), Times.Never);
        _consentimentoRepoMock.Verify(r => r.Atualizar(consentimento), Times.Once);
    }

    [Fact]
    public async Task RevogarConsentimentoAsync_NenhumConsentimentoAtivoParaAFinalidade_LancaNotFoundException()
    {
        var responsavel = CriarResponsavel();
        _repoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _consentimentoRepoMock
            .Setup(r => r.ObterAtivoAsync(responsavel.Id, FinalidadeConsentimento.DadosAgregados))
            .ReturnsAsync((ConsentimentoLgpd?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => CriarServico().RevogarConsentimentoAsync(responsavel.Id, FinalidadeConsentimento.DadosAgregados));
    }

    [Fact]
    public async Task ListarConsentimentosAsync_HistoricoComRevogadoEAtivo_RetornaAmbosOsRegistros()
    {
        var responsavel = CriarResponsavel();
        var revogado = new ConsentimentoLgpd(responsavel.Id, FinalidadeConsentimento.CompartilhamentoRede, DateTime.UtcNow.AddDays(-30));
        revogado.Revogar(DateTime.UtcNow.AddDays(-10));
        var ativo = new ConsentimentoLgpd(responsavel.Id, FinalidadeConsentimento.CompartilhamentoRede, DateTime.UtcNow.AddDays(-5));

        _repoMock.Setup(r => r.ObterPorIdAsync(responsavel.Id)).ReturnsAsync(responsavel);
        _consentimentoRepoMock
            .Setup(r => r.ObterPorResponsavelAsync(responsavel.Id))
            .ReturnsAsync([ativo, revogado]);

        var resultado = (await CriarServico().ListarConsentimentosAsync(responsavel.Id)).ToList();

        Assert.Equal(2, resultado.Count);
        Assert.Contains(resultado, c => c.Ativo);
        Assert.Contains(resultado, c => !c.Ativo && c.DataRevogacao != null);
    }
}
