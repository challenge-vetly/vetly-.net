using Moq;
using Vetly.Application.DTOs.Exame;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do ExameService.
///
/// O eixo destes testes e a RN-104: resultado de exame so chega ao Responsavel
/// depois que o veterinario o libera. Antes disso ele e um numero sem leitura
/// clinica, e o app nao e o lugar de descobrir sozinho o que ele significa.
/// </summary>
public class ExameServiceTests
{
    private readonly Mock<IExameRepository> _repoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private readonly Mock<INotificacaoService> _notificacoesMock = new();
    private readonly Mock<IUsuarioAtual> _usuarioMock = new();

    private ExameService CriarServico() =>
        new(_repoMock.Object, _animalRepoMock.Object, _notificacoesMock.Object, _usuarioMock.Object);

    /// <summary>Coloca o token no papel de Responsavel dono do animal informado.</summary>
    private void ComoTutor(Guid tutorId)
    {
        _usuarioMock.SetupGet(u => u.EhTutor).Returns(true);
        _usuarioMock.SetupGet(u => u.TutorId).Returns(tutorId);
    }

    /// <summary>Coloca o token no papel de veterinario.</summary>
    private void ComoVeterinario(Guid veterinarioId)
    {
        _usuarioMock.SetupGet(u => u.EhVeterinario).Returns(true);
        _usuarioMock.SetupGet(u => u.VeterinarioId).Returns(veterinarioId);
    }

    private static Animal CriarAnimal(Guid tutorId) =>
        new("Thor", "Canino", "Golden Retriever",
            new DateTime(2023, 4, 10, 0, 0, 0, DateTimeKind.Utc), tutorId);

    // ── RN-104: leitura pelo Responsavel ────────────────────────────────────

    [Fact]
    public async Task ObterPorId_TutorDono_ExameNaoLiberado_LancaAcessoNegadoRN104()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);
        var exame = new Exame(animal.Id, Guid.NewGuid(), "Hemograma");
        exame.RegistrarResultado("Leucocitos 22.000/uL");

        _repoMock.Setup(r => r.ObterPorIdAsync(exame.Id)).ReturnsAsync(exame);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        ComoTutor(tutorId);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterPorIdAsync(exame.Id));

        // 403 e nao 404: o Responsavel sabe que o exame existe — foi ele quem o pediu.
        // O que ainda nao existe para ele e o resultado interpretado.
        Assert.Equal("RN-104", ex.Codigo);
    }

    [Fact]
    public async Task ObterPorId_TutorDono_ExameLiberado_DevolveResultado()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);
        var exame = new Exame(animal.Id, Guid.NewGuid(), "Hemograma");
        exame.RegistrarResultado("Leucocitos 22.000/uL");
        exame.LiberarAoTutor();

        _repoMock.Setup(r => r.ObterPorIdAsync(exame.Id)).ReturnsAsync(exame);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        ComoTutor(tutorId);

        var resultado = await CriarServico().ObterPorIdAsync(exame.Id);

        Assert.Equal("Leucocitos 22.000/uL", resultado.Resultado);
        Assert.True(resultado.LiberadoAoTutor);
    }

    [Fact]
    public async Task ObterTodos_ComoTutor_OmiteExamesNaoLiberados()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);

        var liberado = new Exame(animal.Id, Guid.NewGuid(), "Ultrassom");
        liberado.RegistrarResultado("Sem alteracoes");
        liberado.LiberarAoTutor();

        var pendente = new Exame(animal.Id, Guid.NewGuid(), "Hemograma");
        pendente.RegistrarResultado("Leucocitos 22.000/uL");

        _animalRepoMock.Setup(r => r.ObterPorTutorAsync(tutorId)).ReturnsAsync([animal]);
        _repoMock.Setup(r => r.ObterPorAnimalAsync(animal.Id)).ReturnsAsync([liberado, pendente]);
        ComoTutor(tutorId);

        var resultado = (await CriarServico().ObterTodosAsync()).ToList();

        Assert.Single(resultado);
        Assert.Equal(liberado.Id, resultado[0].Id);
    }

    [Fact]
    public async Task ObterPorId_VeterinarioDeOutroExame_LancaAcessoNegadoRN105()
    {
        var exame = new Exame(Guid.NewGuid(), Guid.NewGuid(), "Hemograma");

        _repoMock.Setup(r => r.ObterPorIdAsync(exame.Id)).ReturnsAsync(exame);
        ComoVeterinario(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterPorIdAsync(exame.Id));

        Assert.Equal("RN-105", ex.Codigo);
    }

    // ── RN-105: escrita e do solicitante ────────────────────────────────────

    [Fact]
    public async Task RegistrarResultado_ComoTutor_LancaAcessoNegadoRN105()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);
        var exame = new Exame(animal.Id, Guid.NewGuid(), "Hemograma");

        _repoMock.Setup(r => r.ObterPorIdAsync(exame.Id)).ReturnsAsync(exame);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        ComoTutor(tutorId);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().RegistrarResultadoAsync(exame.Id, "Tudo certo"));

        // Deixar o Responsavel escrever o proprio resultado esvaziaria a RN-104
        Assert.Equal("RN-105", ex.Codigo);
        _repoMock.Verify(r => r.SalvarAsync(), Times.Never);
    }

    [Fact]
    public async Task LiberarAoTutor_VeterinarioQueNaoSolicitou_LancaAcessoNegadoRN105()
    {
        var exame = new Exame(Guid.NewGuid(), Guid.NewGuid(), "Hemograma");
        exame.RegistrarResultado("Leucocitos 22.000/uL");

        _repoMock.Setup(r => r.ObterPorIdAsync(exame.Id)).ReturnsAsync(exame);
        ComoVeterinario(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().LiberarAoTutorAsync(exame.Id));

        Assert.Equal("RN-105", ex.Codigo);
        Assert.False(exame.LiberadoAoTutor);
    }

    [Fact]
    public async Task CriarAsync_ComoVeterinario_IgnoraOIdDoCorpoEUsaODoToken()
    {
        var vetId = Guid.NewGuid();
        var animal = CriarAnimal(Guid.NewGuid());

        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Exame>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        ComoVeterinario(vetId);

        var resultado = await CriarServico().CriarAsync(new CriarExameDto
        {
            AnimalId = animal.Id,
            VeterinarioId = Guid.NewGuid(), // um profissional qualquer, vindo do cliente
            TipoSolicitacao = "Hemograma"
        });

        // Pedir exame no nome de outro deixaria o historico do animal apontando para
        // quem nao participou do atendimento (RN-105)
        Assert.Equal(vetId, resultado.VeterinarioId);
    }

    // ── RN-103/RN-104: notificacoes ─────────────────────────────────────────

    [Fact]
    public async Task CriarAsync_NotificaOResponsavelComOrientacoesDePreparo()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);

        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Exame>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

        await CriarServico().CriarAsync(new CriarExameDto
        {
            AnimalId = animal.Id,
            VeterinarioId = Guid.NewGuid(),
            TipoSolicitacao = "Hemograma"
        });

        // Jejum e coleta tem janela: avisar depois do exame nao serve para nada (RN-103)
        _notificacoesMock.Verify(n => n.CriarAsync(It.Is<CriarNotificacaoDto>(
            d => d.TutorId == tutorId && d.Tipo == TipoNotificacao.ExameSolicitado)), Times.Once);
    }

    [Fact]
    public async Task RegistrarResultado_NaoNotificaAntesDaLiberacao()
    {
        var exame = new Exame(Guid.NewGuid(), Guid.NewGuid(), "Hemograma");

        _repoMock.Setup(r => r.ObterPorIdAsync(exame.Id)).ReturnsAsync(exame);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Exame>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

        await CriarServico().RegistrarResultadoAsync(exame.Id, "Leucocitos 22.000/uL");

        // Avisar agora faria o Responsavel abrir o app e nao encontrar nada (RN-104)
        _notificacoesMock.Verify(n => n.CriarAsync(It.IsAny<CriarNotificacaoDto>()), Times.Never);
    }

    [Fact]
    public async Task LiberarAoTutor_NotificaOResponsavel()
    {
        var tutorId = Guid.NewGuid();
        var animal = CriarAnimal(tutorId);
        var exame = new Exame(animal.Id, Guid.NewGuid(), "Hemograma");
        exame.RegistrarResultado("Leucocitos 22.000/uL");

        _repoMock.Setup(r => r.ObterPorIdAsync(exame.Id)).ReturnsAsync(exame);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Exame>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

        await CriarServico().LiberarAoTutorAsync(exame.Id);

        Assert.True(exame.LiberadoAoTutor);
        _notificacoesMock.Verify(n => n.CriarAsync(It.Is<CriarNotificacaoDto>(
            d => d.TutorId == tutorId && d.Tipo == TipoNotificacao.DocumentoPublicado)), Times.Once);
    }

    // ── RN-103: midias do resultado ─────────────────────────────────────────

    [Fact]
    public async Task RegistrarResultado_ComMidias_DevolveOsIdsAnexados()
    {
        var exame = new Exame(Guid.NewGuid(), Guid.NewGuid(), "Ultrassom");
        var midia = Guid.NewGuid();

        _repoMock.Setup(r => r.ObterPorIdAsync(exame.Id)).ReturnsAsync(exame);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Exame>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

        var resultado = await CriarServico().RegistrarResultadoAsync(
            exame.Id, "Imagem sem alteracoes", [midia]);

        // Laudo de imagem sem a imagem e meia informacao (RN-103)
        Assert.Equal([midia], resultado.MidiaIds);
    }

    [Fact]
    public void AnexarMidias_ListaVazia_GravaSentinela()
    {
        var exame = new Exame(Guid.NewGuid(), Guid.NewGuid(), "Hemograma");

        exame.AnexarMidias([]);

        // Oracle le string vazia como NULL: a sentinela ";" preserva a diferenca entre
        // "nao anexou nada" e "campo nunca preenchido"
        Assert.Equal(";", exame.MidiaIds);
        Assert.Empty(exame.Midias());
    }
}
