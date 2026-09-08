using Microsoft.EntityFrameworkCore;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// Token de concorrência da consulta (RN-006).
///
/// <para>
/// <b>O que aconteceu.</b> Numa validação contra o ambiente real, o webhook confirmou
/// o pagamento e, no mesmo segundo, o Responsável gravou os pré-sintomas. As duas
/// transações carregaram a consulta antes de qualquer uma salvar. O EF grava a linha
/// inteira, então a segunda escrita devolveu ao banco a visão velha que carregara —
/// e desfez a confirmação.
/// </para>
/// <para>
/// O estado final foi pagamento <c>Confirmado</c> com consulta em <c>EmCheckout</c>:
/// o Responsável pagou, o horário depois expirou pela rotina de locks, e não havia
/// erro em lugar nenhum explicando o sumiço. Perda silenciosa na transição mais
/// importante do produto.
/// </para>
/// </summary>
public class ConcorrenciaDaConsultaTests
{
    /// <summary>
    /// Contexto sobre um banco InMemory próprio, com a checagem de concorrência ligada.
    ///
    /// O InMemory só honra token de concorrência com
    /// <c>EnableNullabilityCheck</c>/<c>ConfigureWarnings</c> padrão e a propriedade
    /// marcada — que é o caso aqui. Não substitui o Oracle, mas prova a mecânica do
    /// modelo: a versão entra no <c>WHERE</c> e a escrita defasada não encontra linha.
    /// </summary>
    private static VetlyDbContext NovoContexto(string banco) =>
        new(new DbContextOptionsBuilder<VetlyDbContext>()
            .UseInMemoryDatabase(banco)
            .Options);

    private static Consulta ConsultaEmCheckout() =>
        Consulta.ParaCheckout(
            DateTime.UtcNow.AddDays(1), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Gravar_IncrementaAVersaoDaConsulta()
    {
        var banco = $"versao-{Guid.NewGuid()}";
        var consulta = ConsultaEmCheckout();

        await using (var ctx = NovoContexto(banco))
        {
            ctx.Consultas.Add(consulta);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = NovoContexto(banco))
        {
            var carregada = await ctx.Consultas.FirstAsync(c => c.Id == consulta.Id);
            var antes = carregada.Versao;

            carregada.ConfirmarPagamento();
            await ctx.SaveChangesAsync();

            Assert.Equal(antes + 1, carregada.Versao);
        }
    }

    [Fact]
    public async Task DuasTransacoesConcorrentes_ASegundaFalhaEmVezDeSobrescrever()
    {
        // É exatamente o cenário do incidente: as duas carregam a consulta, a primeira
        // confirma o pagamento e a segunda grava outra coisa a partir da visão velha.
        var banco = $"corrida-{Guid.NewGuid()}";
        var consulta = ConsultaEmCheckout();

        await using (var ctx = NovoContexto(banco))
        {
            ctx.Consultas.Add(consulta);
            await ctx.SaveChangesAsync();
        }

        await using var primeira = NovoContexto(banco);
        await using var segunda = NovoContexto(banco);

        var doWebhook = await primeira.Consultas.FirstAsync(c => c.Id == consulta.Id);
        var doResponsavel = await segunda.Consultas.FirstAsync(c => c.Id == consulta.Id);

        doWebhook.ConfirmarPagamento();
        await primeira.SaveChangesAsync();

        doResponsavel.RegistrarPreSintomas("Vomito ha 2 dias", []);

        // Antes do token, esta linha passava e devolvia a consulta a EmCheckout,
        // apagando a confirmacao que a primeira transacao acabara de gravar.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => segunda.SaveChangesAsync());
    }

    [Fact]
    public async Task DepoisDoConflito_AConfirmacaoDoPagamentoPermanece()
    {
        // O que importa nao e a excecao em si: e a confirmacao ter sobrevivido.
        var banco = $"sobrevive-{Guid.NewGuid()}";
        var consulta = ConsultaEmCheckout();

        await using (var ctx = NovoContexto(banco))
        {
            ctx.Consultas.Add(consulta);
            await ctx.SaveChangesAsync();
        }

        await using (var primeira = NovoContexto(banco))
        await using (var segunda = NovoContexto(banco))
        {
            var doWebhook = await primeira.Consultas.FirstAsync(c => c.Id == consulta.Id);
            var doResponsavel = await segunda.Consultas.FirstAsync(c => c.Id == consulta.Id);

            doWebhook.ConfirmarPagamento();
            await primeira.SaveChangesAsync();

            doResponsavel.RegistrarPreSintomas("Vomito ha 2 dias", []);

            try { await segunda.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) { /* esperado */ }
        }

        await using var conferencia = NovoContexto(banco);
        var final = await conferencia.Consultas.FirstAsync(c => c.Id == consulta.Id);

        Assert.Equal(StatusConsulta.Confirmada, final.Status);
        Assert.Equal(StatusPagamento.Confirmado, final.StatusPagamento);
    }
}
