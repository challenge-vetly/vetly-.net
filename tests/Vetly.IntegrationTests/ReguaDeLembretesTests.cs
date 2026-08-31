using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Jobs;

namespace Vetly.IntegrationTests;

/// <summary>
/// A regua que transforma obrigacao vencendo em aviso ao Responsavel
/// (RN-045/RN-094/RN-095).
///
/// Sem ela, o board de obrigacoes e uma tela que so quem abre o app descobre — e quem
/// ja esqueceu da vacina e exatamente quem nao abre.
///
/// Fica neste projeto porque a rotina vive na Infrastructure.
/// </summary>
public class ReguaDeLembretesTests
{
    private readonly Mock<IObrigacaoRepository> _obrigacoes = new();
    private readonly Mock<INotificacaoRepository> _notificacoes = new();
    private readonly Mock<ILembreteRepository> _lembretes = new();

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly Guid _animalId = Guid.NewGuid();

    private readonly List<Notificacao> _criadas = [];

    public ReguaDeLembretesTests()
    {
        _notificacoes.Setup(r => r.AdicionarAsync(It.IsAny<Notificacao>()))
            .Callback<Notificacao>(_criadas.Add).Returns(Task.CompletedTask);

        _notificacoes.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _notificacoes.Setup(r => r.ObterDoAnimalPorTipoDesdeAsync(
                It.IsAny<Guid>(), It.IsAny<TipoNotificacao>(), It.IsAny<DateTime>()))
            .ReturnsAsync((Notificacao?)null);

        _lembretes.Setup(r => r.AdicionarAsync(It.IsAny<LembreteAgendado>())).Returns(Task.CompletedTask);
        _lembretes.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        _obrigacoes.Setup(r => r.ObterVencendoAteAsync(It.IsAny<DateTime>())).ReturnsAsync([]);
    }

    private AvisarObrigacoesVencendo CriarRotina() =>
        new(_obrigacoes.Object, _notificacoes.Object, _lembretes.Object,
            NullLogger<AvisarObrigacoesVencendo>.Instance);

    private ObrigacaoPet Obrigacao(int venceEmDias, string descricao, Guid? animalId = null) =>
        new(animalId ?? _animalId, _tutorId, TipoObrigacaoPet.Vacina, descricao,
            DateTime.UtcNow.AddDays(venceEmDias), 365);

    private void Vencendo(params ObrigacaoPet[] obrigacoes) =>
        _obrigacoes.Setup(r => r.ObterVencendoAteAsync(It.IsAny<DateTime>())).ReturnsAsync(obrigacoes);

    [Fact]
    public async Task Regua_ComObrigacaoVencendo_CriaAvisoELembrete()
    {
        Vencendo(Obrigacao(10, "V10"));

        var criados = await CriarRotina().ExecutarAsync(CancellationToken.None);

        Assert.Equal(1, criados);
        Assert.Single(_criadas);
        Assert.Equal(TipoNotificacao.ObrigacaoVencendo, _criadas[0].Tipo);

        // O lembrete e o que sustenta a regua: tres tentativas sem resposta acionam o
        // alerta a clinica (RN-095)
        _lembretes.Verify(r => r.AdicionarAsync(It.IsAny<LembreteAgendado>()), Times.Once);
    }

    [Fact]
    public async Task Regua_AgrupaPorAnimalEmVezDeAvisarPorObrigacao()
    {
        Vencendo(Obrigacao(5, "V10"), Obrigacao(12, "Antirrabica"), Obrigacao(20, "Giardia"));

        var criados = await CriarRotina().ExecutarAsync(CancellationToken.None);

        // Tres vacinas vencendo na mesma semana sao um aviso, nao tres
        Assert.Equal(1, criados);
        Assert.Single(_criadas);
    }

    [Fact]
    public async Task Regua_NomeiaAObrigacaoMaisUrgente()
    {
        Vencendo(Obrigacao(20, "Giardia"), Obrigacao(3, "Antirrabica"));

        await CriarRotina().ExecutarAsync(CancellationToken.None);

        // Aviso generico e aviso que nao move ninguem
        Assert.Contains("Antirrabica", _criadas[0].Corpo);
        Assert.Contains("mais 1", _criadas[0].Corpo);
    }

    [Fact]
    public async Task Regua_ComObrigacaoVencida_MudaOTomDoAviso()
    {
        Vencendo(Obrigacao(-5, "Antirrabica"));

        await CriarRotina().ExecutarAsync(CancellationToken.None);

        Assert.Equal("Cuidados em atraso", _criadas[0].Titulo);
        Assert.Contains("em atraso", _criadas[0].Corpo);
    }

    [Fact]
    public async Task Regua_ComAvisoRecente_NaoRepete()
    {
        Vencendo(Obrigacao(10, "V10"));

        _notificacoes.Setup(r => r.ObterDoAnimalPorTipoDesdeAsync(
                _animalId, TipoNotificacao.ObrigacaoVencendo, It.IsAny<DateTime>()))
            .ReturnsAsync(new Notificacao(_tutorId, TipoNotificacao.ObrigacaoVencendo, "t", "c"));

        var criados = await CriarRotina().ExecutarAsync(CancellationToken.None);

        // Avisar de hora em hora sobre a mesma vacina transformaria cuidado em
        // incomodo, e o Responsavel desligaria a notificacao inteira
        Assert.Equal(0, criados);
        Assert.Empty(_criadas);
    }

    [Fact]
    public async Task Regua_AvisaCadaAnimalSeparadamente()
    {
        var outroAnimal = Guid.NewGuid();
        Vencendo(Obrigacao(5, "V10"), Obrigacao(8, "Antirrabica", outroAnimal));

        var criados = await CriarRotina().ExecutarAsync(CancellationToken.None);

        Assert.Equal(2, criados);
        Assert.Equal(2, _criadas.Select(n => n.AnimalId).Distinct().Count());
    }

    [Fact]
    public async Task Regua_LevaODestinoDoBoardDoAnimal()
    {
        Vencendo(Obrigacao(10, "V10"));

        await CriarRotina().ExecutarAsync(CancellationToken.None);

        Assert.Equal($"/animais/{_animalId}/obrigacoes", _criadas[0].Destino);
    }

    [Fact]
    public async Task Regua_SemObrigacaoVencendo_NaoGravaNada()
    {
        var criados = await CriarRotina().ExecutarAsync(CancellationToken.None);

        Assert.Equal(0, criados);
        _notificacoes.Verify(r => r.SalvarAsync(), Times.Never);
    }
}
