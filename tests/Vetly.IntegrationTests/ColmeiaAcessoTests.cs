using Microsoft.EntityFrameworkCore;
using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;
using Vetly.Infrastructure.Repositories;

namespace Vetly.IntegrationTests;

/// <summary>
/// A colmeia abrindo — e fechando — o acesso ao historico do animal (RN-090/RN-105).
///
/// Fica neste projeto porque exercita o repositorio real sobre o DbContext: a
/// vigencia da concessao e uma consulta, e e ela que decide o acesso.
/// </summary>
public class ColmeiaAcessoTests
{
    private static VetlyDbContext CriarContexto(string nome) =>
        new(new DbContextOptionsBuilder<VetlyDbContext>().UseInMemoryDatabase(nome).Options);

    private readonly Guid _tutorId = Guid.NewGuid();
    private readonly Guid _animalId = Guid.NewGuid();
    private readonly Guid _vetDeFora = Guid.NewGuid();

    /// <summary>Cenario: um veterinario que nunca atendeu este animal.</summary>
    private (AnimalService servico, VetlyDbContext contexto, ColmeiaRepository repo) Cenario(string nome)
    {
        var contexto = CriarContexto(nome);

        var animal = new Animal("Thor", "Canino", "SRD", new DateTime(2022, 3, 1), _tutorId);
        typeof(Animal).GetProperty(nameof(Animal.Id))!.SetValue(animal, _animalId);
        contexto.Animais.Add(animal);
        contexto.SaveChanges();

        var colmeiaRepo = new ColmeiaRepository(contexto);

        var usuario = new Mock<IUsuarioAtual>();
        usuario.SetupGet(u => u.EhVeterinario).Returns(true);
        usuario.SetupGet(u => u.VeterinarioId).Returns(_vetDeFora);

        var animalRepo = new Mock<IAnimalRepository>();
        animalRepo.Setup(r => r.ObterPorIdAsync(_animalId)).ReturnsAsync(animal);
        animalRepo.Setup(r => r.VeterinarioAtendeAnimalAsync(_vetDeFora, _animalId)).ReturnsAsync(false);
        animalRepo.Setup(r => r.ObterHistoricoLongitudinalAsync(_animalId)).ReturnsAsync([]);

        var colmeia = new ColmeiaService(
            colmeiaRepo, animalRepo.Object, Mock.Of<IVeterinarioRepository>(), usuario.Object);

        return (new AnimalService(animalRepo.Object, colmeia, usuario.Object), contexto, colmeiaRepo);
    }

    private async Task ConcederAsync(ColmeiaRepository repo, EscopoAcessoColmeia escopo, TimeSpan? validade = null)
    {
        await repo.AdicionarAsync(new AcessoColmeia(_animalId, _tutorId, _vetDeFora, escopo, validade));
        await repo.SalvarAsync();
    }

    [Fact]
    public async Task SemAutorizacao_OVeterinarioDeForaNaoAlcancaOHistorico()
    {
        var (servico, contexto, _) = Cenario($"colmeia-{Guid.NewGuid():N}");
        using var _c = contexto;

        await Assert.ThrowsAsync<AcessoNegadoException>(() => servico.ObterHistoricoAsync(_animalId));
    }

    [Fact]
    public async Task ComAutorizacaoDoResponsavel_OVeterinarioDeForaAlcanca()
    {
        var (servico, contexto, repo) = Cenario($"colmeia-{Guid.NewGuid():N}");
        using var _c = contexto;

        await ConcederAsync(repo, EscopoAcessoColmeia.HistoricoCompleto);

        var historico = await servico.ObterHistoricoAsync(_animalId);

        Assert.Empty(historico);
    }

    [Fact]
    public async Task AutorizacaoRevogada_FechaOAcessoDeNovo()
    {
        var (servico, contexto, repo) = Cenario($"colmeia-{Guid.NewGuid():N}");
        using var _c = contexto;

        await ConcederAsync(repo, EscopoAcessoColmeia.HistoricoCompleto);
        await servico.ObterHistoricoAsync(_animalId);

        var acesso = await repo.ObterVigenteAsync(_animalId, _vetDeFora, DateTime.UtcNow);
        acesso!.Revogar();
        repo.Atualizar(acesso);
        await repo.SalvarAsync();

        await Assert.ThrowsAsync<AcessoNegadoException>(() => servico.ObterHistoricoAsync(_animalId));
    }

    [Fact]
    public async Task AutorizacaoDeEscopoMenor_NaoAbreOHistoricoInteiro()
    {
        var (servico, contexto, repo) = Cenario($"colmeia-{Guid.NewGuid():N}");
        using var _c = contexto;

        await ConcederAsync(repo, EscopoAcessoColmeia.Documentos);

        // Autorizar os documentos nao autoriza o prontuario desde filhote
        await Assert.ThrowsAsync<AcessoNegadoException>(() => servico.ObterHistoricoAsync(_animalId));
    }

    [Fact]
    public async Task TodoAcesso_PermitidoOuNegado_FicaNaTrilha()
    {
        var (servico, contexto, repo) = Cenario($"colmeia-{Guid.NewGuid():N}");
        using var _c = contexto;

        await Assert.ThrowsAsync<AcessoNegadoException>(() => servico.ObterHistoricoAsync(_animalId));

        await ConcederAsync(repo, EscopoAcessoColmeia.HistoricoCompleto);
        await servico.ObterHistoricoAsync(_animalId);

        var trilha = (await repo.ObterLogDoAnimalAsync(_animalId)).ToList();

        // Autorizacao sem registro seria um cheque em branco
        Assert.Equal(2, trilha.Count);
        Assert.Contains(trilha, l => !l.Permitido);
        Assert.Contains(trilha, l => l.Permitido);
        Assert.All(trilha, l => Assert.Equal(_vetDeFora, l.VeterinarioId));
    }

    [Fact]
    public async Task AutorizacaoVencida_NaoAbreNada()
    {
        var (servico, contexto, repo) = Cenario($"colmeia-{Guid.NewGuid():N}");
        using var _c = contexto;

        var vencida = new AcessoColmeia(
            _animalId, _tutorId, _vetDeFora, EscopoAcessoColmeia.HistoricoCompleto, TimeSpan.FromDays(1));

        // Vence antes de agora: acesso clinico expira sozinho, e a consulta de vigencia
        // e quem faz valer
        typeof(AcessoColmeia).GetProperty(nameof(AcessoColmeia.ExpiraEm))!
            .SetValue(vencida, DateTime.UtcNow.AddMinutes(-1));

        await repo.AdicionarAsync(vencida);
        await repo.SalvarAsync();

        await Assert.ThrowsAsync<AcessoNegadoException>(() => servico.ObterHistoricoAsync(_animalId));
    }
}
