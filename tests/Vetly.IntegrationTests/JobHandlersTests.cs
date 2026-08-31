using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vetly.Application.DTOs.ListaEspera;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;
using Vetly.Infrastructure.Jobs;

namespace Vetly.IntegrationTests;

/// <summary>
/// Handlers e rotinas do worker de negocio (§11). Ficam neste projeto porque vivem na
/// Infrastructure e alguns tocam o DbContext.
/// </summary>
public class JobHandlersTests
{
    private static VetlyDbContext CriarContexto(string nome) =>
        new(new DbContextOptionsBuilder<VetlyDbContext>().UseInMemoryDatabase(nome).Options);

    // ── Handler da lista de espera (RN-037) ──────────────────────────────────

    [Fact]
    public async Task PromoverListaEspera_ChamaAPromocaoDoHorario()
    {
        var slotId = Guid.NewGuid();
        var servico = new Mock<IListaEsperaService>();
        servico.Setup(s => s.PromoverProximoAsync(slotId))
            .ReturnsAsync(new ItemListaEsperaDto { Id = Guid.NewGuid(), PrioridadeAte = DateTime.UtcNow.AddMinutes(15) });

        var handler = new PromoverListaEsperaHandler(
            servico.Object, NullLogger<PromoverListaEsperaHandler>.Instance);

        await handler.ExecutarAsync(new Job(TipoJob.PromoverListaEspera, slotId.ToString()), CancellationToken.None);

        servico.Verify(s => s.PromoverProximoAsync(slotId), Times.Once);
    }

    [Fact]
    public async Task PromoverListaEspera_FilaVazia_NaoEFalha()
    {
        var servico = new Mock<IListaEsperaService>();
        servico.Setup(s => s.PromoverProximoAsync(It.IsAny<Guid>())).ReturnsAsync((ItemListaEsperaDto?)null);

        var handler = new PromoverListaEsperaHandler(
            servico.Object, NullLogger<PromoverListaEsperaHandler>.Instance);

        // Horario liberado sem ninguem esperando e situacao normal, nao erro
        await handler.ExecutarAsync(
            new Job(TipoJob.PromoverListaEspera, Guid.NewGuid().ToString()), CancellationToken.None);
    }

    [Fact]
    public async Task PromoverListaEspera_PayloadInvalido_Falha()
    {
        var handler = new PromoverListaEsperaHandler(
            Mock.Of<IListaEsperaService>(), NullLogger<PromoverListaEsperaHandler>.Instance);

        // Falhar aqui e o certo: o worker retenta e, esgotado, registra o job como falho
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.ExecutarAsync(new Job(TipoJob.PromoverListaEspera, "nao-e-guid"), CancellationToken.None));
    }

    // ── Handler do pagamento simulado (§5.1) ─────────────────────────────────

    [Fact]
    public async Task ConfirmarPagamentoSimulado_EntregaOEventoPeloMesmoCaminhoDoProvedor()
    {
        var pagamentos = new Mock<IPagamentoService>();
        pagamentos
            .Setup(p => p.ProcessarWebhookAsync(It.IsAny<string>(), "token-de-servico"))
            .ReturnsAsync(new ResultadoDoWebhookDto());

        var token = new Mock<ITokenDeServico>();
        token.SetupGet(t => t.Valor).Returns("token-de-servico");

        var handler = new ConfirmarPagamentoSimuladoHandler(
            pagamentos.Object, token.Object, NullLogger<ConfirmarPagamentoSimuladoHandler>.Instance);

        var payload = """{"referenciaExterna":"sim_abc","status":"Confirmado"}""";
        await handler.ExecutarAsync(new Job(TipoJob.ConfirmarPagamentoSimulado, payload), CancellationToken.None);

        // Entra pelo mesmo processamento de webhook que um provedor real usaria — nao ha
        // porta de tras que dispense a autenticacao de servico
        pagamentos.Verify(p => p.ProcessarWebhookAsync(payload, "token-de-servico"), Times.Once);
    }

    [Fact]
    public async Task ConfirmarPagamentoSimulado_PayloadVazio_Falha()
    {
        var handler = new ConfirmarPagamentoSimuladoHandler(
            Mock.Of<IPagamentoService>(), Mock.Of<ITokenDeServico>(),
            NullLogger<ConfirmarPagamentoSimuladoHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.ExecutarAsync(new Job(TipoJob.ConfirmarPagamentoSimulado), CancellationToken.None));
    }

    // ── Rotina de expiração de lock (RN-035/RN-037) ──────────────────────────

    [Fact]
    public async Task ExpirarLocks_HorarioComLockVencido_VoltaAFicarLivre()
    {
        var banco = $"jobs_{Guid.NewGuid()}";
        Guid slotId;

        await using (var ctx = CriarContexto(banco))
        {
            var slot = new Slot(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddMinutes(30));
            var consulta = Consulta.ParaCheckout(
                slot.Inicio, slot.VeterinarioId, Guid.NewGuid(), Guid.NewGuid(), slot.Id, Guid.NewGuid());

            // Lock aberto ha 20 minutos: ja venceu
            slot.TravarParaCheckout(consulta.Id, DateTime.UtcNow.AddMinutes(-20));

            ctx.Slots.Add(slot);
            ctx.Consultas.Add(consulta);
            await ctx.SaveChangesAsync();
            slotId = slot.Id;
        }

        var fila = new Mock<IFilaDeJobs>();

        await using (var ctx = CriarContexto(banco))
        {
            var rotina = new ExpirarLocksDeCheckout(ctx, fila.Object, NullLogger<ExpirarLocksDeCheckout>.Instance);
            var afetados = await rotina.ExecutarAsync(CancellationToken.None);

            Assert.Equal(1, afetados);
        }

        await using (var verificacao = CriarContexto(banco))
        {
            var slot = await verificacao.Slots.FirstAsync(s => s.Id == slotId);
            var consulta = await verificacao.Consultas.FirstAsync(c => c.SlotId == slotId);

            Assert.Equal(EstadoSlot.Livre, slot.Estado);
            Assert.Null(slot.LockConsultaId);
            Assert.Equal(StatusConsulta.Expirada, consulta.Status);
        }

        // Toda entrada em "livre" avisa a lista de espera (RN-037)
        fila.Verify(f => f.EnfileirarAsync(TipoJob.PromoverListaEspera, slotId.ToString(), null), Times.Once);
    }

    [Fact]
    public async Task ExpirarLocks_LockAindaValido_NaoEMexido()
    {
        var banco = $"jobs_{Guid.NewGuid()}";

        await using (var ctx = CriarContexto(banco))
        {
            var slot = new Slot(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddMinutes(30));
            slot.TravarParaCheckout(Guid.NewGuid(), DateTime.UtcNow);
            ctx.Slots.Add(slot);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CriarContexto(banco))
        {
            var rotina = new ExpirarLocksDeCheckout(
                ctx, Mock.Of<IFilaDeJobs>(), NullLogger<ExpirarLocksDeCheckout>.Instance);

            Assert.Equal(0, await rotina.ExecutarAsync(CancellationToken.None));
        }
    }

    [Fact]
    public async Task ExpirarLocks_ConsultaJaConfirmada_NaoEDesfeita()
    {
        var banco = $"jobs_{Guid.NewGuid()}";
        Guid consultaId;

        await using (var ctx = CriarContexto(banco))
        {
            var slot = new Slot(Guid.NewGuid(), DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddMinutes(30));
            var consulta = Consulta.ParaCheckout(
                slot.Inicio, slot.VeterinarioId, Guid.NewGuid(), Guid.NewGuid(), slot.Id, Guid.NewGuid());

            slot.TravarParaCheckout(consulta.Id, DateTime.UtcNow.AddMinutes(-20));

            // O pagamento entrou no meio do caminho e a consulta ja foi confirmada
            consulta.ConfirmarPagamento();

            ctx.Slots.Add(slot);
            ctx.Consultas.Add(consulta);
            await ctx.SaveChangesAsync();
            consultaId = consulta.Id;
        }

        await using (var ctx = CriarContexto(banco))
        {
            var rotina = new ExpirarLocksDeCheckout(
                ctx, Mock.Of<IFilaDeJobs>(), NullLogger<ExpirarLocksDeCheckout>.Instance);
            await rotina.ExecutarAsync(CancellationToken.None);
        }

        await using (var verificacao = CriarContexto(banco))
        {
            var consulta = await verificacao.Consultas.FirstAsync(c => c.Id == consultaId);

            // Expirar uma consulta ja paga seria desfazer um agendamento valido
            Assert.Equal(StatusConsulta.Confirmada, consulta.Status);
        }
    }

    // ── Rotina de limpeza da idempotência (§2.5/§6.5) ────────────────────────

    [Fact]
    public async Task LimparIdempotencia_RemoveSoOsVencidos()
    {
        var banco = $"jobs_{Guid.NewGuid()}";

        await using (var ctx = CriarContexto(banco))
        {
            var vigente = new RegistroIdempotencia("chave-vigente", Guid.NewGuid(), "POST /api/pagamentos", 202, "{}");

            // Forca o vencimento por reflexao: a entidade nao expoe alterar a expiracao,
            // e nao deveria — quem define a validade e a propria regra dos 24h
            var vencido = new RegistroIdempotencia("chave-vencida", Guid.NewGuid(), "POST /api/pagamentos", 202, "{}");
            typeof(RegistroIdempotencia)
                .GetProperty(nameof(RegistroIdempotencia.ExpiraEm))!
                .SetValue(vencido, DateTime.UtcNow.AddHours(-1));

            ctx.RegistrosDeIdempotencia.AddRange(vigente, vencido);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CriarContexto(banco))
        {
            var rotina = new LimparIdempotenciaVencida(ctx, NullLogger<LimparIdempotenciaVencida>.Instance);

            Assert.Equal(1, await rotina.ExecutarAsync(CancellationToken.None));
        }

        await using (var verificacao = CriarContexto(banco))
        {
            var restantes = await verificacao.RegistrosDeIdempotencia.ToListAsync();

            Assert.Single(restantes);
            Assert.Equal("chave-vigente", restantes[0].Chave);
        }
    }
}
