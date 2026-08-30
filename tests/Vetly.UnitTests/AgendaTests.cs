using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Testes da configuracao de agenda e da maquina de estados do slot
/// (RN-034/RN-035/RN-037).
/// </summary>
public class AgendaTests
{
    private static AgendaConfig ConfigPadrao(
        DiasDaSemana dias = DiasDaSemana.DiasUteis,
        int inicio = 8 * 60, int fim = 12 * 60, int duracao = 30, int intervalo = 0) =>
        new(Guid.NewGuid(), dias, inicio, fim, duracao, intervalo);

    // ── Configuração da agenda (RN-034) ──────────────────────────────────────

    [Fact]
    public void Configurar_SemDiaDeAtendimento_NaoEAceita()
    {
        Assert.Throws<ArgumentException>(() => ConfigPadrao(DiasDaSemana.Nenhum));
    }

    [Fact]
    public void Configurar_FimAntesDoInicio_NaoEAceito()
    {
        Assert.Throws<ArgumentException>(() => ConfigPadrao(inicio: 18 * 60, fim: 9 * 60));
    }

    [Fact]
    public void Configurar_ExpedienteCurtoDemaisParaUmAtendimento_NaoEAceito()
    {
        // Uma agenda que nao produz nenhum horario deixaria o perfil invisivel na
        // busca sem que o veterinario entendesse por que
        Assert.Throws<ArgumentException>(() => ConfigPadrao(inicio: 8 * 60, fim: 8 * 60 + 20, duracao: 30));
    }

    // ── Geração de horários ──────────────────────────────────────────────────

    [Fact]
    public void GerarHorariosDoDia_DiaSemAtendimento_NaoGeraNada()
    {
        var config = ConfigPadrao(DiasDaSemana.DiasUteis);
        var domingo = new DateTime(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(DayOfWeek.Sunday, domingo.DayOfWeek);
        Assert.Empty(config.GerarHorariosDoDia(domingo));
    }

    [Fact]
    public void GerarHorariosDoDia_DivideOExpedientePelaDuracao()
    {
        // 8h as 12h, 30 min por atendimento, sem intervalo => 8 horarios
        var config = ConfigPadrao(duracao: 30, intervalo: 0);
        var segunda = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

        var horarios = config.GerarHorariosDoDia(segunda).ToList();

        Assert.Equal(8, horarios.Count);
        Assert.Equal(new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc), horarios[0].Inicio);
        Assert.Equal(new DateTime(2026, 9, 7, 8, 30, 0, DateTimeKind.Utc), horarios[0].Fim);
        Assert.Equal(new DateTime(2026, 9, 7, 11, 30, 0, DateTimeKind.Utc), horarios[^1].Inicio);
    }

    [Fact]
    public void GerarHorariosDoDia_RespeitaOIntervaloEntreAtendimentos()
    {
        // 8h as 12h, 30 min de atendimento + 15 de intervalo => passo de 45 min
        var config = ConfigPadrao(duracao: 30, intervalo: 15);
        var segunda = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

        var horarios = config.GerarHorariosDoDia(segunda).ToList();

        Assert.Equal(new DateTime(2026, 9, 7, 8, 45, 0, DateTimeKind.Utc), horarios[1].Inicio);
        // O ultimo atendimento tem que caber inteiro dentro do expediente
        Assert.True(horarios[^1].Fim <= new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GerarHorariosDoDia_UltimoAtendimentoNaoUltrapassaOExpediente()
    {
        // 8h as 12h20 com 30 min: o horario das 12h nao cabe e nao pode ser gerado
        var config = ConfigPadrao(inicio: 8 * 60, fim: 12 * 60 + 20, duracao: 30, intervalo: 0);
        var segunda = new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc);

        var horarios = config.GerarHorariosDoDia(segunda).ToList();

        Assert.All(horarios, h => Assert.True(h.Fim <= new DateTime(2026, 9, 7, 12, 20, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData(DayOfWeek.Monday, true)]
    [InlineData(DayOfWeek.Friday, true)]
    [InlineData(DayOfWeek.Saturday, false)]
    [InlineData(DayOfWeek.Sunday, false)]
    public void DiasUteis_CobreDeSegundaASexta(DayOfWeek dia, bool esperado)
    {
        Assert.Equal(esperado, DiasDaSemana.DiasUteis.Atende(dia));
    }

    // ── Máquina de estados do slot (RN-035) ──────────────────────────────────

    private static Slot NovoSlot() => new(
        Guid.NewGuid(),
        new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 9, 7, 8, 30, 0, DateTimeKind.Utc));

    [Fact]
    public void Slot_NasceLivre()
    {
        var slot = NovoSlot();

        Assert.Equal(EstadoSlot.Livre, slot.Estado);
        Assert.True(slot.EstaDisponivel(DateTime.UtcNow));
    }

    [Fact]
    public void TravarParaCheckout_ReservaPorDezMinutos()
    {
        var slot = NovoSlot();
        var agora = DateTime.UtcNow;
        var consultaId = Guid.NewGuid();

        Assert.True(slot.TravarParaCheckout(consultaId, agora));

        Assert.Equal(EstadoSlot.EmCheckout, slot.Estado);
        Assert.Equal(agora.AddMinutes(10), slot.LockAte);
        Assert.Equal(consultaId, slot.LockConsultaId);
    }

    [Fact]
    public void TravarParaCheckout_SlotJaTravado_RecusaOSegundoCheckout()
    {
        var slot = NovoSlot();
        var agora = DateTime.UtcNow;
        slot.TravarParaCheckout(Guid.NewGuid(), agora);

        // Quem chegou depois nao leva o horario — e o que impede overbooking
        Assert.False(slot.TravarParaCheckout(Guid.NewGuid(), agora.AddMinutes(5)));
    }

    [Fact]
    public void TravarParaCheckout_LockVencido_LiberaParaOProximo()
    {
        var slot = NovoSlot();
        var agora = DateTime.UtcNow;
        slot.TravarParaCheckout(Guid.NewGuid(), agora);

        // Passados os 10 minutos o horario volta a valer, sem depender do job de
        // expiracao: a condicao e avaliada na leitura
        var depoisDoLock = agora.AddMinutes(11);

        Assert.True(slot.EstaDisponivel(depoisDoLock));
        Assert.True(slot.TravarParaCheckout(Guid.NewGuid(), depoisDoLock));
    }

    [Fact]
    public void Confirmar_OcupaOHorarioEmDefinitivo()
    {
        var slot = NovoSlot();
        slot.TravarParaCheckout(Guid.NewGuid(), DateTime.UtcNow);

        slot.Confirmar();

        Assert.Equal(EstadoSlot.Confirmado, slot.Estado);
        Assert.Null(slot.LockAte);
        Assert.False(slot.EstaDisponivel(DateTime.UtcNow.AddHours(1)));
    }

    [Fact]
    public void Liberar_DevolveOHorarioELimpaOVinculo()
    {
        var slot = NovoSlot();
        slot.TravarParaCheckout(Guid.NewGuid(), DateTime.UtcNow);
        slot.Confirmar();

        slot.Liberar();

        Assert.Equal(EstadoSlot.Livre, slot.Estado);
        Assert.Null(slot.LockConsultaId);
        Assert.True(slot.EstaDisponivel(DateTime.UtcNow));
    }

    [Fact]
    public void Bloquear_HorarioConfirmado_NaoEPermitido()
    {
        var slot = NovoSlot();
        slot.TravarParaCheckout(Guid.NewGuid(), DateTime.UtcNow);
        slot.Confirmar();

        // Bloquear a agenda por cima de consulta confirmada deixaria o Responsavel
        // sem atendimento e sem aviso
        Assert.Throws<InvalidOperationException>(slot.Bloquear);
    }

    [Fact]
    public void Slot_FimAntesDoInicio_NaoEAceito()
    {
        var inicio = new DateTime(2026, 9, 7, 9, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new Slot(Guid.NewGuid(), inicio, inicio.AddMinutes(-30)));
    }

    // ── Serviços (RN-032) ────────────────────────────────────────────────────

    [Fact]
    public void Servico_ValorNegativo_NaoEAceito()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Servico(Guid.NewGuid(), TipoServico.ConsultaRotina, -1m, 30));
    }

    [Fact]
    public void Servico_DuracaoZerada_NaoEAceita()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Servico(Guid.NewGuid(), TipoServico.Banho, 80m, 0));
    }

    [Fact]
    public void Desativar_TiraDaVitrineSemApagar()
    {
        var servico = new Servico(Guid.NewGuid(), TipoServico.ConsultaRotina, 200m, 30);

        servico.Desativar();

        Assert.False(servico.Ativo);
        Assert.Equal(200m, servico.Valor);
    }
}
