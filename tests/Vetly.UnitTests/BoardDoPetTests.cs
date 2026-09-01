using Moq;
using Vetly.Application.DTOs.Obrigacao;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Board do pet e carteira do Responsavel (RN-011/RN-020/RN-041/RN-071/RN-096).
/// </summary>
public class BoardDoPetTests
{
    private readonly Mock<IAnimalRepository> _animalRepo = new();
    private readonly Mock<IColmeiaService> _colmeia = new();
    private readonly Mock<IObrigacaoService> _obrigacoes = new();
    private readonly Mock<IDocumentoRepository> _documentos = new();
    private readonly Mock<IVeterinarioRepository> _vetRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly Animal _animal;
    private readonly Veterinario _vet;

    public BoardDoPetTests()
    {
        _animal = new Animal("Thor", "Canino", "SRD", new DateTime(2022, 3, 1), _tutorId);
        _animal.RegistrarPeso(28m);

        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);

        _animalRepo.Setup(r => r.ObterPorIdAsync(_animal.Id)).ReturnsAsync(_animal);
        _animalRepo.Setup(r => r.ObterConsultasFuturasAsync(_animal.Id, It.IsAny<DateTime>())).ReturnsAsync([]);
        _documentos.Setup(r => r.ObterPublicadosPorAnimalAsync(_animal.Id)).ReturnsAsync([]);
        _vetRepo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);

        SemObrigacoes();
    }

    private AnimalService CriarServico() =>
        new(_animalRepo.Object, _colmeia.Object, _obrigacoes.Object,
            _documentos.Object, _vetRepo.Object, _usuario.Object);

    private void SemObrigacoes() =>
        _obrigacoes.Setup(o => o.ObterBoardAsync(_animal.Id, It.IsAny<bool>()))
            .ReturnsAsync(new BoardDeObrigacoesDto { AnimalId = _animal.Id });

    private void ComObrigacoes(bool temPendencia, params ObrigacaoPetDto[] itens) =>
        _obrigacoes.Setup(o => o.ObterBoardAsync(_animal.Id, It.IsAny<bool>()))
            .ReturnsAsync(new BoardDeObrigacoesDto
            {
                AnimalId = _animal.Id,
                TemPendencia = temPendencia,
                Obrigacoes = [.. itens]
            });

    private static ObrigacaoPetDto Vencida(TipoObrigacaoPet tipo, string descricao) => new()
    {
        Id = Guid.NewGuid(),
        Tipo = tipo,
        Descricao = descricao,
        Situacao = SituacaoObrigacao.Vencida,
        ProximoVencimento = DateTime.UtcNow.AddDays(-5)
    };

    // ── Avatar (RN-020/RN-096/RN-097) ────────────────────────────────────────

    [Fact]
    public async Task Avatar_SemObrigacaoVencida_EstaSaudavel()
    {
        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        Assert.Equal(EstadoDoAvatar.Saudavel, board.AvatarEstado);
    }

    [Fact]
    public async Task Avatar_ComVacinaVencida_FicaAdoentado()
    {
        ComObrigacoes(true, Vencida(TipoObrigacaoPet.Vacina, "Antirrabica"));

        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        Assert.Equal(EstadoDoAvatar.VacinaAtrasada, board.AvatarEstado);
    }

    [Fact]
    public async Task Avatar_VacinaTemPrecedenciaSobreHigiene()
    {
        ComObrigacoes(true,
            Vencida(TipoObrigacaoPet.Antiparasitario, "Antipulgas"),
            Vencida(TipoObrigacaoPet.Vacina, "V10"));

        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        // Antirrabica atrasada e questao sanitaria; banho atrasado e desconforto.
        // Com as duas vencidas, o avatar mostra a que importa mais.
        Assert.Equal(EstadoDoAvatar.VacinaAtrasada, board.AvatarEstado);
    }

    [Fact]
    public async Task Avatar_ComOutraObrigacaoVencida_FicaComHigieneAtrasada()
    {
        ComObrigacoes(true, Vencida(TipoObrigacaoPet.Antiparasitario, "Antipulgas"));

        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        Assert.Equal(EstadoDoAvatar.HigieneAtrasada, board.AvatarEstado);
    }

    // ── Conteúdo do board (RN-011/RN-068/RN-090) ─────────────────────────────

    [Fact]
    public async Task Board_TrazOsProximosAtendimentosComONomeDoProfissional()
    {
        var consulta = Consulta.ParaCheckout(
            DateTime.UtcNow.AddDays(2), _vet.Id, _animal.Id, _tutorId, Guid.NewGuid(), Guid.NewGuid());
        consulta.ConfirmarPagamento();

        _animalRepo.Setup(r => r.ObterConsultasFuturasAsync(_animal.Id, It.IsAny<DateTime>()))
            .ReturnsAsync([consulta]);

        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        var agendamento = Assert.Single(board.ProximosAgendamentos);
        Assert.Equal("Dra. Marina", agendamento.VeterinarioNome);
    }

    [Fact]
    public async Task Board_TrazSempreOsAlertasDeSeguranca()
    {
        _animal.DefinirPerfilClinico(alergias: ["Dipirona"]);
        _animal.AdicionarAlerta("Displasia leve");

        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        // RN-068: alergia e interacao nunca sao ocultaveis
        Assert.Contains("Dipirona", board.AlertasDeSeguranca);
        Assert.Contains("Displasia leve", board.AlertasDeSeguranca);
    }

    [Fact]
    public async Task Board_LimitaOsDocumentosRecentes()
    {
        var docs = Enumerable.Range(0, 9).Select(_ =>
        {
            var d = new Documento(TipoDocumento.Prontuario, "12345-SP", Guid.NewGuid());
            d.RegistrarConteudo("x");
            d.Publicar(DateTime.UtcNow);

            return d;
        }).ToList();

        _documentos.Setup(r => r.ObterPublicadosPorAnimalAsync(_animal.Id)).ReturnsAsync(docs);

        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        // O board e tela de entrada, nao arquivo
        Assert.Equal(5, board.DocumentosRecentes.Count);
    }

    [Fact]
    public async Task Board_DeAnimalAlheio_ERecusado()
    {
        _usuario.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().ObterBoardAsync(_animal.Id));
    }

    [Fact]
    public async Task Board_CalculaAIdadeDoAnimal()
    {
        var board = await CriarServico().ObterBoardAsync(_animal.Id);

        Assert.True(board.IdadeAnos >= 4);
        Assert.Equal(28m, board.PesoKg);
    }
}

/// <summary>
/// Carteira do Responsavel: pagamentos, descontos e reembolsos (RN-041/RN-071).
/// </summary>
public class CarteiraTests
{
    private readonly Mock<IPagamentoRepository> _repo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();

    public CarteiraTests()
    {
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);
        _repo.Setup(r => r.ObterPorTutorAsync(It.IsAny<Guid>())).ReturnsAsync([]);
    }

    private PagamentoService CriarServico() =>
        new(_repo.Object, Mock.Of<IVeterinarioRepository>(), Mock.Of<IConsultaRepository>(),
            Mock.Of<IEmpresaRepository>(), Mock.Of<IPagamentoAdapter>(), Mock.Of<IAgendaRepository>(),
            Mock.Of<IFilaDeJobs>(), [], Mock.Of<IFidelidadeService>(), _usuario.Object,
            Mock.Of<IColmeiaService>());

    private Pagamento Pagamento(decimal valor = 200m, bool confirmado = true, decimal? estorno = null)
    {
        var pagamento = new Pagamento(_tutorId, valor, MeioPagamento.Pix, Guid.NewGuid());

        if (confirmado)
            pagamento.Confirmar();

        if (estorno is { } valorEstornado)
            pagamento.Estornar(valorEstornado);

        return pagamento;
    }

    private void ComLancamentos(params Pagamento[] pagamentos) =>
        _repo.Setup(r => r.ObterPorTutorAsync(_tutorId)).ReturnsAsync(pagamentos);

    [Fact]
    public async Task Carteira_SomaSomenteOQueFoiConfirmado()
    {
        ComLancamentos(Pagamento(), Pagamento(confirmado: false));

        var carteira = await CriarServico().ObterCarteiraAsync(_tutorId);

        // Cobranca pendente ainda nao saiu do bolso de ninguem
        Assert.Equal(200m, carteira.TotalPago);
        Assert.Equal(2, carteira.Lancamentos.Count);
    }

    [Fact]
    public async Task Carteira_SeparaOEstornoDoQueFoiPago()
    {
        ComLancamentos(Pagamento(estorno: 140m));

        var carteira = await CriarServico().ObterCarteiraAsync(_tutorId);

        // Um total unico esconderia a diferenca entre "paguei" e "me devolveram"
        Assert.Equal(140m, carteira.TotalEstornado);
    }

    [Fact]
    public async Task Carteira_DizQueALiquidacaoESimulada()
    {
        var carteira = await CriarServico().ObterCarteiraAsync(_tutorId);

        // RN-071: prometer movimentacao que nao acontece seria pior que nao mostrar
        Assert.Equal("Simulada", carteira.Liquidacao);
    }

    [Fact]
    public async Task Carteira_DeOutroResponsavel_ERecusada()
    {
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterCarteiraAsync(Guid.NewGuid()));

        Assert.Equal("RN-106", ex.Codigo);
    }
}
