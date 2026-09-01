using Moq;
using Vetly.Application.DTOs.Internacao;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do InternacaoService.
/// Cobre calculo de saldo restante na alta e regra de internacao ja encerrada.
/// </summary>
public class InternacaoServiceTests
{
    private readonly Mock<IInternacaoRepository> _repoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private readonly Mock<IPagamentoService> _pagamentosMock = new();
    private readonly Mock<INotificacaoService> _notificacoesMock = new();
    private readonly Mock<IUsuarioAtual> _usuarioMock = new();

    /// <summary>Por padrao os testes rodam como Admin, que alcanca todo o escopo.</summary>
    public InternacaoServiceTests() => _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

    private InternacaoService CriarServico() =>
        new(_repoMock.Object, _animalRepoMock.Object, _pagamentosMock.Object,
            _notificacoesMock.Object, _usuarioMock.Object);

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
        Assert.NotEqual(default, resultado.DataAlta);
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

    // ── RN-105: quem abre, escreve e da alta ────────────────────────────────

    /// <summary>Coloca o token no papel de Responsavel dono do animal informado.</summary>
    private void ComoTutor(Guid tutorId)
    {
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhTutor).Returns(true);
        _usuarioMock.SetupGet(u => u.TutorId).Returns(tutorId);
    }

    private static Animal CriarAnimal(Guid tutorId) =>
        new("Thor", "Canino", "Golden Retriever",
            new DateTime(2023, 4, 10, 0, 0, 0, DateTimeKind.Utc), tutorId);

    [Fact]
    public async Task AbrirAsync_ComoTutor_LancaAcessoNegadoRN105()
    {
        ComoTutor(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().AbrirAsync(new CriarInternacaoDto
            {
                AnimalId = Guid.NewGuid(),
                VeterinarioId = Guid.NewGuid(),
                ValorCaucao = 400m
            }));

        // Internacao e ato clinico: quem interna e quem responde pelo caso
        Assert.Equal("RN-105", ex.Codigo);
        _repoMock.Verify(r => r.AdicionarAsync(It.IsAny<Internacao>()), Times.Never);
    }

    [Fact]
    public async Task DarAltaAsync_ComoTutor_LancaAcessoNegadoRN105()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);
        var internacao = new Internacao(animal.Id, Guid.NewGuid(), valorCaucao: 100m);

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        ComoTutor(tutorId);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().DarAltaAsync(internacao.Id));

        // O Responsavel le a internacao do proprio animal, mas nao a encerra
        Assert.Equal("RN-105", ex.Codigo);
        Assert.Null(internacao.DataAlta);
    }

    [Fact]
    public async Task ObterPorIdAsync_TutorDeOutroAnimal_LancaAcessoNegadoRN105()
    {
        var animal = CriarAnimal(Guid.NewGuid());
        var internacao = new Internacao(animal.Id, Guid.NewGuid(), valorCaucao: 100m);

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        ComoTutor(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterPorIdAsync(internacao.Id));

        Assert.Equal("RN-105", ex.Codigo);
    }

    // ── RN-101: caucao e saldo sao cobranca de verdade ──────────────────────

    [Fact]
    public async Task AbrirAsync_ComCaucao_GeraCobrancaPendenteNoResponsavel()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);

        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ObterAtivaDoAnimalAsync(animal.Id)).ReturnsAsync((Internacao?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Internacao>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _pagamentosMock.Setup(p => p.CriarCobrancaAsync(It.IsAny<CriarPagamentoDto>()))
            .ReturnsAsync(new CobrancaCriadaRespostaDto { Id = Guid.NewGuid(), StatusPagamento = StatusPagamento.Pendente });

        var resultado = await CriarServico().AbrirAsync(new CriarInternacaoDto
        {
            AnimalId = animal.Id,
            VeterinarioId = Guid.NewGuid(),
            ValorCaucao = 400m,
            MeioPagamento = MeioPagamento.Pix
        });

        Assert.Equal(400m, resultado.ValorCaucao);

        // Registrar a caucao so na entidade deixaria dinheiro fora do consolidado
        // financeiro: ela passa pelo mesmo adaptador das consultas (RN-101)
        _pagamentosMock.Verify(p => p.CriarCobrancaAsync(It.Is<CriarPagamentoDto>(
            d => d.TutorId == tutorId && d.Valor == 400m && d.InternacaoId != null)), Times.Once);
    }

    [Fact]
    public async Task AbrirAsync_SemCaucao_NaoGeraCobranca()
    {
        var animal = CriarAnimal(Guid.NewGuid());

        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.ObterAtivaDoAnimalAsync(animal.Id)).ReturnsAsync((Internacao?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Internacao>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        await CriarServico().AbrirAsync(new CriarInternacaoDto
        {
            AnimalId = animal.Id,
            VeterinarioId = Guid.NewGuid(),
            ValorCaucao = 0m
        });

        // Cobranca de zero reais so poluiria o extrato do Responsavel
        _pagamentosMock.Verify(p => p.CriarCobrancaAsync(It.IsAny<CriarPagamentoDto>()), Times.Never);
    }

    [Fact]
    public async Task DarAltaAsync_ComSaldoPositivo_GeraSegundaCobranca()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);
        var internacao = new Internacao(animal.Id, Guid.NewGuid(), valorCaucao: 100m);
        internacao.ApurarValor(300m);

        var cobrancaId = Guid.NewGuid();

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Internacao>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _pagamentosMock.Setup(p => p.CriarCobrancaAsync(It.IsAny<CriarPagamentoDto>()))
            .ReturnsAsync(new CobrancaCriadaRespostaDto { Id = cobrancaId, StatusPagamento = StatusPagamento.Pendente });

        var resultado = await CriarServico().DarAltaAsync(internacao.Id);

        // RN-101/RN-102: o saldo apurado alem da caucao vira cobranca propria
        Assert.Equal(200m, resultado.SaldoRestante);
        Assert.Equal(cobrancaId, resultado.PagamentoDoSaldoId);
        _pagamentosMock.Verify(p => p.CriarCobrancaAsync(It.Is<CriarPagamentoDto>(
            d => d.TutorId == tutorId && d.Valor == 200m && d.InternacaoId == internacao.Id)), Times.Once);
    }

    [Fact]
    public async Task DarAltaAsync_ComSaldoNegativo_NaoCobraDevolucao()
    {
        var animal = CriarAnimal(Guid.NewGuid());
        var internacao = new Internacao(animal.Id, Guid.NewGuid(), valorCaucao: 500m);
        internacao.ApurarValor(300m);

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Internacao>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);

        var resultado = await CriarServico().DarAltaAsync(internacao.Id);

        // Devolucao nao se cobra: fica registrada para o acerto (RN-102)
        Assert.Equal(-200m, resultado.SaldoRestante);
        Assert.Null(resultado.PagamentoDoSaldoId);
        _pagamentosMock.Verify(p => p.CriarCobrancaAsync(It.IsAny<CriarPagamentoDto>()), Times.Never);
    }

    // ── RN-100: o Responsavel acompanha o dia do animal ─────────────────────

    [Fact]
    public async Task RegistrarProcedimentosAsync_NotificaOResponsavel()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);
        var internacao = new Internacao(animal.Id, Guid.NewGuid(), valorCaucao: 500m);

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Internacao>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);

        await CriarServico().RegistrarProcedimentosAsync(internacao.Id, new RegistrarProcedimentosDto
        {
            Procedimentos = [new ProcedimentoDiarioDto { Data = DateTime.UtcNow, Procedimento = "Fluidoterapia", Valor = 120m }]
        });

        // A ausencia de noticia e o que faz o Responsavel ligar tres vezes na clinica
        _notificacoesMock.Verify(n => n.CriarAsync(It.Is<CriarNotificacaoDto>(
            d => d.TutorId == tutorId && d.Tipo == TipoNotificacao.AtualizacaoInternacao)), Times.Once);
    }

    [Fact]
    public async Task DarAltaAsync_NotificaOResponsavelComOSaldo()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);
        var internacao = new Internacao(animal.Id, Guid.NewGuid(), valorCaucao: 100m);
        internacao.ApurarValor(300m);

        _repoMock.Setup(r => r.ObterPorIdAsync(internacao.Id)).ReturnsAsync(internacao);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Internacao>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _pagamentosMock.Setup(p => p.CriarCobrancaAsync(It.IsAny<CriarPagamentoDto>()))
            .ReturnsAsync(new CobrancaCriadaRespostaDto { Id = Guid.NewGuid() });

        await CriarServico().DarAltaAsync(internacao.Id);

        _notificacoesMock.Verify(n => n.CriarAsync(It.Is<CriarNotificacaoDto>(
            d => d.TutorId == tutorId
                 && d.Tipo == TipoNotificacao.AtualizacaoInternacao
                 && d.Corpo.Contains("200"))), Times.Once);
    }
}
