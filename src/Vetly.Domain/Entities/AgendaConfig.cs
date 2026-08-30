using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Configuração da agenda de um veterinário: dias, horário, duração média e intervalo
/// entre atendimentos (RN-034).
///
/// É a partir dela que os <see cref="Slot"/> são materializados — a disponibilidade
/// que o Responsável enxerga na busca não é calculada em tempo de consulta, é linha
/// no banco, para que o lock de checkout (RN-035) tenha o que travar.
/// </summary>
public class AgendaConfig
{
    /// <summary>Horizonte de materialização de slots, em dias.</summary>
    public const int DiasDeHorizonte = 60;

    /// <summary>Identificador único da configuração (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Veterinário dono da agenda.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Dias da semana em que atende.</summary>
    public DiasDaSemana Dias { get; private set; }

    /// <summary>Início do expediente, em minutos desde a meia-noite.</summary>
    public int InicioEmMinutos { get; private set; }

    /// <summary>Fim do expediente, em minutos desde a meia-noite.</summary>
    public int FimEmMinutos { get; private set; }

    /// <summary>Duração média do atendimento, em minutos.</summary>
    public int DuracaoMinutos { get; private set; }

    /// <summary>Intervalo entre atendimentos, em minutos. Pode ser zero.</summary>
    public int IntervaloMinutos { get; private set; }

    /// <summary>Data e hora da última alteração (UTC).</summary>
    public DateTime AtualizadaEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private AgendaConfig() { }

    /// <summary>Cria a configuração de agenda de um veterinário.</summary>
    public AgendaConfig(
        Guid veterinarioId, DiasDaSemana dias,
        int inicioEmMinutos, int fimEmMinutos, int duracaoMinutos, int intervaloMinutos)
    {
        Id = Guid.NewGuid();
        VeterinarioId = veterinarioId;
        Configurar(dias, inicioEmMinutos, fimEmMinutos, duracaoMinutos, intervaloMinutos);
    }

    /// <summary>
    /// Reconfigura a agenda. As invariantes recusam configuração que não produz
    /// nenhum horário — uma agenda vazia seria um perfil invisível na busca sem que
    /// o veterinário entendesse por quê.
    /// </summary>
    public void Configurar(
        DiasDaSemana dias, int inicioEmMinutos, int fimEmMinutos, int duracaoMinutos, int intervaloMinutos)
    {
        if (dias == DiasDaSemana.Nenhum)
            throw new ArgumentException("Informe ao menos um dia de atendimento.", nameof(dias));

        if (inicioEmMinutos is < 0 or >= 1440)
            throw new ArgumentOutOfRangeException(nameof(inicioEmMinutos), "Horário de início inválido.");

        if (fimEmMinutos is < 0 or > 1440)
            throw new ArgumentOutOfRangeException(nameof(fimEmMinutos), "Horário de término inválido.");

        if (fimEmMinutos <= inicioEmMinutos)
            throw new ArgumentException("O fim do expediente deve ser depois do início.", nameof(fimEmMinutos));

        if (duracaoMinutos <= 0)
            throw new ArgumentOutOfRangeException(nameof(duracaoMinutos), "A duração deve ser maior que zero.");

        if (intervaloMinutos < 0)
            throw new ArgumentOutOfRangeException(nameof(intervaloMinutos), "O intervalo não pode ser negativo.");

        if (inicioEmMinutos + duracaoMinutos > fimEmMinutos)
            throw new ArgumentException(
                "O expediente é curto demais para um atendimento da duração configurada.", nameof(duracaoMinutos));

        Dias = dias;
        InicioEmMinutos = inicioEmMinutos;
        FimEmMinutos = fimEmMinutos;
        DuracaoMinutos = duracaoMinutos;
        IntervaloMinutos = intervaloMinutos;
        AtualizadaEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Gera os horários de um dia, respeitando duração e intervalo. Devolve vazio
    /// quando a agenda não atende naquele dia da semana.
    /// </summary>
    /// <param name="dia">Data (em UTC) para a qual gerar os horários.</param>
    public IEnumerable<(DateTime Inicio, DateTime Fim)> GerarHorariosDoDia(DateTime dia)
    {
        if (!Dias.Atende(dia.DayOfWeek))
            yield break;

        var base_ = new DateTime(dia.Year, dia.Month, dia.Day, 0, 0, 0, DateTimeKind.Utc);
        var passo = DuracaoMinutos + IntervaloMinutos;

        for (var minuto = InicioEmMinutos; minuto + DuracaoMinutos <= FimEmMinutos; minuto += passo)
            yield return (base_.AddMinutes(minuto), base_.AddMinutes(minuto + DuracaoMinutos));
    }
}
