using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Agenda;

/// <summary>
/// Configuração da agenda do veterinário: dias, horário, duração e intervalo (RN-034).
/// Salvar materializa os horários dos próximos 60 dias.
/// </summary>
public class ConfigurarAgendaDto
{
    /// <summary>Dias de atendimento. Combináveis: <c>["Segunda","Quarta"]</c>.</summary>
    [Required(ErrorMessage = "Informe ao menos um dia de atendimento.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um dia de atendimento.")]
    public List<DayOfWeek> Dias { get; set; } = [];

    /// <summary>Início do expediente no formato <c>HH:mm</c>.</summary>
    [Required(ErrorMessage = "O horário de início é obrigatório.")]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use o formato HH:mm.")]
    public string HoraInicio { get; set; } = string.Empty;

    /// <summary>Fim do expediente no formato <c>HH:mm</c>.</summary>
    [Required(ErrorMessage = "O horário de término é obrigatório.")]
    [RegularExpression(@"^([01]\d|2[0-3]):[0-5]\d$", ErrorMessage = "Use o formato HH:mm.")]
    public string HoraFim { get; set; } = string.Empty;

    /// <summary>Duração média do atendimento, em minutos.</summary>
    [Range(5, 480, ErrorMessage = "A duração deve estar entre 5 e 480 minutos.")]
    public int DuracaoMinutos { get; set; }

    /// <summary>Intervalo entre atendimentos, em minutos.</summary>
    [Range(0, 240, ErrorMessage = "O intervalo deve estar entre 0 e 240 minutos.")]
    public int IntervaloMinutos { get; set; }
}

/// <summary>Configuração de agenda vigente e o efeito da última materialização.</summary>
public class AgendaConfigDto
{
    public Guid VeterinarioId { get; set; }
    public List<DayOfWeek> Dias { get; set; } = [];
    public string HoraInicio { get; set; } = string.Empty;
    public string HoraFim { get; set; } = string.Empty;
    public int DuracaoMinutos { get; set; }
    public int IntervaloMinutos { get; set; }
    public DateTime AtualizadaEm { get; set; }

    /// <summary>Quantidade de horários criados na última materialização.</summary>
    public int SlotsMaterializados { get; set; }

    /// <summary>Até quando a agenda está materializada.</summary>
    public DateTime MaterializadaAte { get; set; }
}

/// <summary>Um horário da agenda, como o Responsável o vê na hora de escolher.</summary>
public class SlotDto
{
    public Guid Id { get; set; }
    public Guid VeterinarioId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public EstadoSlot Estado { get; set; }
}

/// <summary>Disponibilidade do veterinário agrupada por dia.</summary>
public class DisponibilidadeDto
{
    public Guid VeterinarioId { get; set; }

    /// <summary>Dias com horários livres, em ordem cronológica.</summary>
    public List<DiaDisponivelDto> Dias { get; set; } = [];

    /// <summary>Total de horários livres no período consultado.</summary>
    public int TotalDeHorariosLivres { get; set; }
}

/// <summary>Horários livres de um dia.</summary>
public class DiaDisponivelDto
{
    public DateOnly Data { get; set; }
    public List<SlotDto> Horarios { get; set; } = [];
}

/// <summary>Serviço oferecido pelo prestador (RN-032).</summary>
public class ServicoDto
{
    public Guid Id { get; set; }
    public Guid PrestadorId { get; set; }
    public TipoServico Tipo { get; set; }
    public decimal Valor { get; set; }
    public bool AceitaPlanoPet { get; set; }
    public int DuracaoMinutos { get; set; }
    public bool Ativo { get; set; }
}

/// <summary>Serviço informado no cadastro da vitrine do prestador.</summary>
public class DefinirServicoDto
{
    [Required(ErrorMessage = "O tipo de serviço é obrigatório.")]
    public TipoServico Tipo { get; set; }

    [Range(0, 999999.99, ErrorMessage = "O valor deve estar entre 0 e 999.999,99.")]
    public decimal Valor { get; set; }

    [Range(5, 480, ErrorMessage = "A duração deve estar entre 5 e 480 minutos.")]
    public int DuracaoMinutos { get; set; }

    /// <summary>Indica se o prestador aceita plano de saúde pet neste serviço.</summary>
    public bool AceitaPlanoPet { get; set; }
}

/// <summary>Vitrine de serviços enviada de uma vez pelo prestador.</summary>
public class DefinirServicosDto
{
    [Required(ErrorMessage = "Informe ao menos um serviço.")]
    [MinLength(1, ErrorMessage = "Informe ao menos um serviço.")]
    public List<DefinirServicoDto> Servicos { get; set; } = [];
}
