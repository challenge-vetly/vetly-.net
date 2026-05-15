using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Lembrete;

/// <summary>DTO de entrada para agendamento de um lembrete.</summary>
public class CriarLembreteDto
{
    [Required]
    public Guid AnimalId { get; set; }

    [Required]
    public Guid TutorId { get; set; }

    [Required]
    public TipoLembrete Tipo { get; set; }

    [Required]
    public DateTime DataEvento { get; set; }
}
