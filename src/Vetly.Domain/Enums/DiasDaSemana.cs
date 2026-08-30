namespace Vetly.Domain.Enums;

/// <summary>
/// Dias de atendimento da agenda do veterinário (RN-034).
/// Combinável: uma agenda de segunda a sexta é a soma dos cinco dias.
/// Persistido como um único NUMBER, em vez de cinco colunas ou de uma lista.
/// </summary>
[Flags]
public enum DiasDaSemana
{
    Nenhum = 0,
    Domingo = 1,
    Segunda = 2,
    Terca = 4,
    Quarta = 8,
    Quinta = 16,
    Sexta = 32,
    Sabado = 64,

    /// <summary>Segunda a sexta — a configuração mais comum.</summary>
    DiasUteis = Segunda | Terca | Quarta | Quinta | Sexta,

    /// <summary>Todos os dias da semana.</summary>
    TodosOsDias = Domingo | DiasUteis | Sabado
}

/// <summary>Conversões entre <see cref="DiasDaSemana"/> e <see cref="DayOfWeek"/>.</summary>
public static class DiasDaSemanaExtensions
{
    /// <summary>Converte um dia do calendário para o flag correspondente.</summary>
    public static DiasDaSemana ParaFlag(this DayOfWeek dia) => dia switch
    {
        DayOfWeek.Sunday => DiasDaSemana.Domingo,
        DayOfWeek.Monday => DiasDaSemana.Segunda,
        DayOfWeek.Tuesday => DiasDaSemana.Terca,
        DayOfWeek.Wednesday => DiasDaSemana.Quarta,
        DayOfWeek.Thursday => DiasDaSemana.Quinta,
        DayOfWeek.Friday => DiasDaSemana.Sexta,
        DayOfWeek.Saturday => DiasDaSemana.Sabado,
        _ => DiasDaSemana.Nenhum
    };

    /// <summary>Indica se a agenda atende no dia informado.</summary>
    public static bool Atende(this DiasDaSemana dias, DayOfWeek dia) => dias.HasFlag(dia.ParaFlag());
}
