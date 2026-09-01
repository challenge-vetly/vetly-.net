using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>Pedido de agendamento de retorno (RN-013).</summary>
public class AgendarRetornoDto
{
    /// <summary>Horário do retorno, na agenda do mesmo profissional.</summary>
    [Required(ErrorMessage = "O horário do retorno é obrigatório.")]
    public Guid SlotId { get; set; }

    /// <summary>
    /// Por que o retorno foi marcado. Vai para a notificação do Responsável — "revisar
    /// a cicatrização" o faz voltar; "retorno" sozinho, não.
    /// </summary>
    [MaxLength(300, ErrorMessage = "O motivo deve ter no máximo 300 caracteres.")]
    public string? Motivo { get; set; }
}

/// <summary>Retorno agendado.</summary>
public class RetornoAgendadoDto
{
    /// <summary>Consulta criada para o retorno.</summary>
    public Guid ConsultaId { get; set; }

    /// <summary>Atendimento que originou o retorno.</summary>
    public Guid ConsultaOrigemId { get; set; }

    public DateTime DataHora { get; set; }

    public Guid VeterinarioId { get; set; }
    public Guid AnimalId { get; set; }

    /// <summary>
    /// Até quando o profissional segue alcançando o histórico do animal. Nulo quando
    /// não havia autorização de colmeia a estender — o retorno acontece com a visão
    /// restrita ao que ele mesmo produziu (RN-066/RN-090).
    /// </summary>
    public DateTime? ColmeiaEstendidaAte { get; set; }
}
