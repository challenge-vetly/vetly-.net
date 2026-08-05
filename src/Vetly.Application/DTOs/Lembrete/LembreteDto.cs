using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Lembrete;

/// <summary>DTO de resposta com os dados de um lembrete agendado.</summary>
public class LembreteDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public Guid ResponsavelId { get; set; }
    public TipoLembrete Tipo { get; set; }
    public DateTime DataEvento { get; set; }
    public int TentativasRealizadas { get; set; }
    public bool ResponsavelRespondeu { get; set; }
    public bool AlertaEnviadoClinica { get; set; }
}
