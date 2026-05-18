using Moq;
using Vetly.Application.DTOs.Internacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do InternacaoService.
/// Cobre calculo de saldo restante na alta e regra de internacao ja encerrada.
/// </summary>
public class InternacaoServiceTests
{
    private readonly Mock<IInternacaoRepository> _repoMock = new();

    private InternacaoService CriarServico() => new(_repoMock.Object);

    [Fact]
    public async Task DarAltaAsync_CalculaSaldoRestante_Corretamente()
    {
        var internacao = new Internacao(Guid.NewGuid(), Guid.NewGuid(), valorCaucao: 100m);
        internacao.ApurarValor(300m);

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Internacao>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().DarAltaAsync(internacao.Id);

        Assert.Equal(100m, resultado.ValorCaucao);
        Assert.Equal(300m, resultado.ValorTotalApurado);
        Assert.Equal(200m, resultado.SaldoRestante);
        Assert.NotNull(resultado.DataAlta);
    }

    [Fact]
    public async Task DarAltaAsync_InternacaoJaEncerrada_LancaInvalidOperationException()
    {
        var internacao = new Internacao(Guid.NewGuid(), Guid.NewGuid(), valorCaucao: 100m);
        internacao.DarAlta();

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CriarServico().DarAltaAsync(internacao.Id));
    }

    [Fact]
    public async Task RegistrarProcedimentosAsync_SomaValoresAoTotalApurado()
    {
        var internacao = new Internacao(Guid.NewGuid(), Guid.NewGuid(), valorCaucao: 500m);

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Internacao>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = new RegistrarProcedimentosDto
        {
            Procedimentos =
            [
                new ProcedimentoDiarioDto { Data = DateTime.UtcNow, Procedimento = "Soro IV", Valor = 150m },
                new ProcedimentoDiarioDto { Data = DateTime.UtcNow, Procedimento = "Antibiotico", Valor = 275m }
            ]
        };

        var resultado = await CriarServico().RegistrarProcedimentosAsync(internacao.Id, dto);

        Assert.Equal(425m, resultado.ValorTotalApurado);
    }

    [Fact]
    public async Task RegistrarProcedimentosAsync_AcumulaAoLongoDeMultiplasChamadas()
    {
        var internacao = new Internacao(Guid.NewGuid(), Guid.NewGuid(), valorCaucao: 500m);

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Internacao>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var service = CriarServico();

        await service.RegistrarProcedimentosAsync(internacao.Id, new RegistrarProcedimentosDto
        {
            Procedimentos = [new ProcedimentoDiarioDto { Data = DateTime.UtcNow, Procedimento = "Dia 1", Valor = 200m }]
        });
        var final = await service.RegistrarProcedimentosAsync(internacao.Id, new RegistrarProcedimentosDto
        {
            Procedimentos = [new ProcedimentoDiarioDto { Data = DateTime.UtcNow, Procedimento = "Dia 2", Valor = 300m }]
        });

        Assert.Equal(500m, final.ValorTotalApurado);
    }
}
