using Microsoft.EntityFrameworkCore;
using Vetly.Application.Exceptions;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;
using Vetly.Infrastructure.Repositories;

namespace Vetly.IntegrationTests;

/// <summary>
/// Concorrencia no horario da agenda (RN-035).
///
/// A guarda em memoria do <see cref="Slot"/> resolve a corrida dentro de uma
/// requisicao, mas nao a corrida entre duas: dois processos podem ler o mesmo horario
/// Livre no mesmo milissegundo e gravar EmCheckout os dois — a ultima gravacao vence,
/// e dois animais vao para o mesmo horario. Estes testes provam que ESTADO e
/// LOCK_CONSULTA_ID sao tokens de concorrencia e que a colisao vira 409 na fronteira
/// do repositorio, sem que a Application precise conhecer o EF Core.
/// </summary>
public class ConcorrenciaDeSlotTests
{
    private static VetlyDbContext CriarContexto(string nome) =>
        new(new DbContextOptionsBuilder<VetlyDbContext>()
            .UseInMemoryDatabase(nome)
            .Options);

    /// <summary>Materializa um horario livre e devolve o id.</summary>
    private static async Task<Guid> SemearSlotLivreAsync(string nomeDoBanco)
    {
        await using var ctx = CriarContexto(nomeDoBanco);

        var slot = new Slot(Guid.NewGuid(), DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddMinutes(30));

        ctx.Slots.Add(slot);
        await ctx.SaveChangesAsync();

        return slot.Id;
    }

    [Fact]
    public async Task DoisCheckoutsSimultaneos_OSegundoRecebeConflitoDeEstadoRN035()
    {
        var nomeDoBanco = $"slot_concorrencia_{Guid.NewGuid()}";
        var slotId = await SemearSlotLivreAsync(nomeDoBanco);

        // Duas requisicoes independentes: cada uma com o proprio contexto, como em
        // producao. As duas leem o horario ainda Livre.
        await using var contextoA = CriarContexto(nomeDoBanco);
        await using var contextoB = CriarContexto(nomeDoBanco);

        var repoA = new AgendaRepository(contextoA);
        var repoB = new AgendaRepository(contextoB);

        var slotA = await repoA.ObterSlotAsync(slotId);
        var slotB = await repoB.ObterSlotAsync(slotId);

        Assert.NotNull(slotA);
        Assert.NotNull(slotB);

        var consultaA = Guid.NewGuid();
        var consultaB = Guid.NewGuid();

        // A guarda em memoria deixa as duas passarem: para cada contexto, o horario
        // lido estava mesmo livre
        Assert.True(slotA!.TravarParaCheckout(consultaA, DateTime.UtcNow));
        Assert.True(slotB!.TravarParaCheckout(consultaB, DateTime.UtcNow));

        repoA.AtualizarSlot(slotA);
        await repoA.SalvarAsync();

        repoB.AtualizarSlot(slotB);

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(() => repoB.SalvarAsync());

        Assert.Equal("RN-035", ex.Codigo);

        // O horario ficou com quem chegou primeiro
        await using var conferencia = CriarContexto(nomeDoBanco);
        var persistido = await conferencia.Slots.FirstAsync(s => s.Id == slotId);

        Assert.Equal(EstadoSlot.EmCheckout, persistido.Estado);
        Assert.Equal(consultaA, persistido.LockConsultaId);
    }

    [Fact]
    public async Task ConfirmacaoConcorrenteComLiberacao_NaoSobrescreveOEstadoLido()
    {
        var nomeDoBanco = $"slot_confirmacao_{Guid.NewGuid()}";
        var slotId = await SemearSlotLivreAsync(nomeDoBanco);
        var consultaId = Guid.NewGuid();

        // O horario entra em checkout
        await using (var ctx = CriarContexto(nomeDoBanco))
        {
            var repo = new AgendaRepository(ctx);
            var slot = await repo.ObterSlotAsync(slotId);
            slot!.TravarParaCheckout(consultaId, DateTime.UtcNow);
            repo.AtualizarSlot(slot);
            await repo.SalvarAsync();
        }

        await using var contextoWebhook = CriarContexto(nomeDoBanco);
        await using var contextoExpiracao = CriarContexto(nomeDoBanco);

        var repoWebhook = new AgendaRepository(contextoWebhook);
        var repoExpiracao = new AgendaRepository(contextoExpiracao);

        var slotWebhook = await repoWebhook.ObterSlotAsync(slotId);
        var slotExpiracao = await repoExpiracao.ObterSlotAsync(slotId);

        // A rotina de expiracao libera o horario primeiro
        slotExpiracao!.Liberar();
        repoExpiracao.AtualizarSlot(slotExpiracao);
        await repoExpiracao.SalvarAsync();

        // O webhook, que leu o horario ainda em checkout, tenta confirma-lo
        slotWebhook!.Confirmar();
        repoWebhook.AtualizarSlot(slotWebhook);

        var ex = await Assert.ThrowsAsync<ConflitoDeEstadoException>(() => repoWebhook.SalvarAsync());

        Assert.Equal("RN-035", ex.Codigo);

        // Sem o token, o horario terminaria Confirmado sem lock nenhum — ocupado por
        // uma consulta que ja tinha perdido a vez
        await using var conferencia = CriarContexto(nomeDoBanco);
        var persistido = await conferencia.Slots.FirstAsync(s => s.Id == slotId);

        Assert.Equal(EstadoSlot.Livre, persistido.Estado);
        Assert.Null(persistido.LockConsultaId);
    }

    [Fact]
    public async Task GravacaoSemConcorrencia_SegueFuncionando()
    {
        var nomeDoBanco = $"slot_sem_conflito_{Guid.NewGuid()}";
        var slotId = await SemearSlotLivreAsync(nomeDoBanco);
        var consultaId = Guid.NewGuid();

        await using var ctx = CriarContexto(nomeDoBanco);
        var repo = new AgendaRepository(ctx);

        var slot = await repo.ObterSlotAsync(slotId);
        Assert.True(slot!.TravarParaCheckout(consultaId, DateTime.UtcNow));

        repo.AtualizarSlot(slot);

        // O token so atrapalha quem perdeu a corrida: o caminho normal nao muda
        Assert.Equal(1, await repo.SalvarAsync());
    }
}
