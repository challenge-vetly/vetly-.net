using Moq;
using Vetly.Application.DTOs.Obrigacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Obrigacoes de cuidado do animal e o board que as mostra (RN-045/RN-046).
/// </summary>
public class ObrigacaoPetTests
{
    private readonly Mock<IObrigacaoRepository> _repo = new();
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly Animal _animal;

    public ObrigacaoPetTests()
    {
        _animal = new Animal("Thor", "Canino", "SRD", new DateTime(2022, 3, 1), _tutorId);

        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);

        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(_animal);
        _repo.Setup(r => r.AdicionarAsync(It.IsAny<ObrigacaoPet>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _repo.Setup(r => r.ObterDoAnimalAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync([]);
    }

    private ObrigacaoService CriarServico() =>
        new(_repo.Object, _animalRepo.Object, _usuario.Object);

    private ObrigacaoPet Obrigacao(
        int venceEmDias,
        TipoObrigacaoPet tipo = TipoObrigacaoPet.Vacina,
        string descricao = "V10",
        int periodicidade = 365)
    {
        var obrigacao = new ObrigacaoPet(
            _animal.Id, _tutorId, tipo, descricao,
            DateTime.UtcNow.AddDays(venceEmDias), periodicidade);

        _repo.Setup(r => r.ObterPorIdAsync(obrigacao.Id)).ReturnsAsync(obrigacao);

        return obrigacao;
    }

    private void NoBoard(params ObrigacaoPet[] obrigacoes) =>
        _repo.Setup(r => r.ObterDoAnimalAsync(_animal.Id, It.IsAny<bool>())).ReturnsAsync(obrigacoes);

    // ── Situação (RN-045) ────────────────────────────────────────────────────

    [Fact]
    public void Obrigacao_ComVencimentoDistante_EstaEmDia()
    {
        var obrigacao = Obrigacao(venceEmDias: 200);

        Assert.Equal(SituacaoObrigacao.EmDia, obrigacao.SituacaoEm(DateTime.UtcNow));
    }

    [Fact]
    public void Obrigacao_DentroDaJanelaDeAviso_JaApareceComoVencendo()
    {
        var obrigacao = Obrigacao(venceEmDias: 20);

        // Avisar so no vencimento e avisar tarde: agendar consulta leva dias
        Assert.Equal(SituacaoObrigacao.Vencendo, obrigacao.SituacaoEm(DateTime.UtcNow));
    }

    [Fact]
    public void Obrigacao_NoDiaSeguinteAoVencimento_EstaVencida()
    {
        var obrigacao = Obrigacao(venceEmDias: -1);

        Assert.Equal(SituacaoObrigacao.Vencida, obrigacao.SituacaoEm(DateTime.UtcNow));
        Assert.True(obrigacao.DiasAteVencer(DateTime.UtcNow) <= 0);
    }

    [Fact]
    public void Obrigacao_ExatamenteNaBordaDaJanela_AindaEEmDia()
    {
        var agora = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        var obrigacao = new ObrigacaoPet(
            _animal.Id, _tutorId, TipoObrigacaoPet.Vacina, "V10",
            agora.Add(ObrigacaoPet.JanelaDeAviso).AddSeconds(1), 365);

        Assert.Equal(SituacaoObrigacao.EmDia, obrigacao.SituacaoEm(agora));
    }

    // ── Cumprimento (RN-045) ─────────────────────────────────────────────────

    [Fact]
    public void Cumprir_ContaOProximoVencimentoAPartirDoCumprimento()
    {
        var vencidaHaDoisMeses = new ObrigacaoPet(
            _animal.Id, _tutorId, TipoObrigacaoPet.Vacina, "V10",
            DateTime.UtcNow.AddDays(-60), periodicidadeEmDias: 365);

        var cumpridaHoje = DateTime.UtcNow;
        vencidaHaDoisMeses.Cumprir(cumpridaHoje);

        // Quem vacinou com dois meses de atraso nao deve receber o proximo aviso dois
        // meses adiantado
        Assert.Equal(cumpridaHoje.AddDays(365).Date, vencidaHaDoisMeses.ProximoVencimento.Date);
        Assert.Equal(SituacaoObrigacao.EmDia, vencidaHaDoisMeses.SituacaoEm(DateTime.UtcNow));
    }

    [Fact]
    public void Cumprir_ObrigacaoDeUmaVezSo_ArquivaEmVezDeFicarVencida()
    {
        var retorno = new ObrigacaoPet(
            _animal.Id, _tutorId, TipoObrigacaoPet.Retorno, "Retorno em 15 dias",
            DateTime.UtcNow.AddDays(15), periodicidadeEmDias: 0);

        retorno.Cumprir(DateTime.UtcNow);

        Assert.True(retorno.Arquivada);
        Assert.Equal(SituacaoObrigacao.Arquivada, retorno.SituacaoEm(DateTime.UtcNow));
    }

    [Fact]
    public async Task Cumprir_RegistraQuemCumpriuEEmQualConsulta()
    {
        var vetId = Guid.NewGuid();
        _usuario.SetupGet(u => u.VeterinarioId).Returns(vetId);

        var obrigacao = Obrigacao(venceEmDias: 5);
        var consultaId = Guid.NewGuid();

        await CriarServico().CumprirAsync(obrigacao.Id, new CumprirObrigacaoDto { ConsultaId = consultaId });

        Assert.Equal(vetId, obrigacao.RegistradaPorVeterinarioId);
        Assert.Equal(consultaId, obrigacao.UltimaConsultaId);
    }

    [Fact]
    public async Task Cumprir_NoFuturo_NaoEAceito()
    {
        var obrigacao = Obrigacao(venceEmDias: 30);

        // Registro de algo que nao aconteceu, e que empurraria o proximo vencimento
        // para longe demais
        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().CumprirAsync(obrigacao.Id, new CumprirObrigacaoDto
            {
                Quando = DateTime.UtcNow.AddDays(10)
            }));
    }

    // ── Board (RN-045) ───────────────────────────────────────────────────────

    [Fact]
    public async Task Board_ContaPorSituacaoEAcendeOAvisoQuandoHaAtraso()
    {
        NoBoard(
            Obrigacao(-10, descricao: "Antirrabica"),
            Obrigacao(15, descricao: "V10"),
            Obrigacao(200, descricao: "Giardia"));

        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        Assert.Equal(1, board.TotalVencidas);
        Assert.Equal(1, board.TotalVencendo);
        Assert.Equal(1, board.TotalEmDia);

        // A primeira pergunta do Responsavel nao e "quais sao", e "tem algo atrasado?"
        Assert.True(board.TemPendencia);
    }

    [Fact]
    public async Task Board_TrazAsMaisUrgentesNaFrente()
    {
        NoBoard(
            Obrigacao(200, descricao: "Giardia"),
            Obrigacao(-10, descricao: "Antirrabica"),
            Obrigacao(15, descricao: "V10"));

        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        // O board serve para decidir o que fazer, nao para catalogar
        Assert.Equal("Antirrabica", board.Obrigacoes[0].Descricao);
        Assert.Equal("V10", board.Obrigacoes[1].Descricao);
    }

    [Fact]
    public async Task Board_SemObrigacoes_NaoAcendeAviso()
    {
        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        Assert.Empty(board.Obrigacoes);
        Assert.False(board.TemPendencia);
    }

    [Fact]
    public async Task Board_DeAnimalAlheio_ERecusado()
    {
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterBoardAsync(_animal.Id));

        Assert.Equal("RN-105", ex.Codigo);
    }

    [Fact]
    public async Task Board_DeAnimalQueOVeterinarioNaoAtende_ERecusado()
    {
        var vetId = Guid.NewGuid();
        _usuario.SetupGet(u => u.EhTutor).Returns(false);
        _usuario.SetupGet(u => u.EhVeterinario).Returns(true);
        _usuario.SetupGet(u => u.VeterinarioId).Returns(vetId);
        _animalRepo.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, _animal.Id)).ReturnsAsync(false);

        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().ObterBoardAsync(_animal.Id));
    }

    // ── Derivação da carteira (RN-046) ───────────────────────────────────────

    [Fact]
    public async Task Derivar_CriaUmaObrigacaoPorTipoDeVacina()
    {
        _animal.DefinirCarteiraVacinacao(
        [
            new RegistroVacinacao("V10", DateTime.UtcNow.AddDays(-300)),
            new RegistroVacinacao("Antirrabica", DateTime.UtcNow.AddDays(-200))
        ]);

        var criadas = await CriarServico().DerivarDaCarteiraAsync(_animal.Id);

        Assert.Equal(2, criadas.Count());
        Assert.All(criadas, o => Assert.True(o.DerivadaDaCarteira));
    }

    [Fact]
    public async Task Derivar_ContaAPartirDaDoseMaisRecenteDoMesmoTipo()
    {
        var ultimaDose = DateTime.UtcNow.AddDays(-100);

        _animal.DefinirCarteiraVacinacao(
        [
            new RegistroVacinacao("V10", DateTime.UtcNow.AddDays(-500)),
            new RegistroVacinacao("V10", ultimaDose)
        ]);

        var criadas = (await CriarServico().DerivarDaCarteiraAsync(_animal.Id)).ToList();

        // Doses antigas do mesmo tipo sao historico, nao obrigacoes separadas
        var obrigacao = Assert.Single(criadas);
        Assert.Equal(ultimaDose.AddDays(365).Date, obrigacao.ProximoVencimento.Date);
    }

    [Fact]
    public async Task Derivar_DuasVezes_NaoDuplica()
    {
        _animal.DefinirCarteiraVacinacao([new RegistroVacinacao("V10", DateTime.UtcNow.AddDays(-100))]);

        var jaExiste = new ObrigacaoPet(
            _animal.Id, _tutorId, TipoObrigacaoPet.Vacina, "V10", DateTime.UtcNow.AddDays(265), 365);

        _repo.Setup(r => r.ObterDoAnimalAsync(_animal.Id, true)).ReturnsAsync([jaExiste]);

        var criadas = await CriarServico().DerivarDaCarteiraAsync(_animal.Id);

        Assert.Empty(criadas);
    }

    [Fact]
    public async Task Derivar_CarteiraVazia_NaoCriaNada()
    {
        var criadas = await CriarServico().DerivarDaCarteiraAsync(_animal.Id);

        Assert.Empty(criadas);
        _repo.Verify(r => r.SalvarAsync(), Times.Never);
    }

    // ── Arquivamento ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Arquivar_TiraDoBoardSemApagarDoHistorico()
    {
        var obrigacao = Obrigacao(venceEmDias: 10);

        var resultado = await CriarServico().ArquivarAsync(obrigacao.Id);

        Assert.True(resultado.Arquivada);
        Assert.Equal(SituacaoObrigacao.Arquivada, resultado.Situacao);
        Assert.NotNull(obrigacao.Descricao);
    }

    [Fact]
    public void Obrigacao_SemDescricao_NaoEAceita()
    {
        // "Vacina" sozinho nao diz qual vacina precisa ser aplicada
        Assert.Throws<ArgumentException>(() => new ObrigacaoPet(
            _animal.Id, _tutorId, TipoObrigacaoPet.Vacina, "   ", DateTime.UtcNow.AddDays(30)));
    }

    [Fact]
    public void Obrigacao_ComPeriodicidadeNegativa_NaoEAceita()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ObrigacaoPet(
            _animal.Id, _tutorId, TipoObrigacaoPet.Vacina, "V10", DateTime.UtcNow.AddDays(30), -1));
    }
}
