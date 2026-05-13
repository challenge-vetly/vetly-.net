using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>DTO de resposta com os dados de uma consulta.</summary>
public class ConsultaDto
{
    public Guid Id { get; set; }
    public DateTime DataHora { get; set; }
    public ModalidadeAtendimento Modalidade { get; set; }
    public Guid VeterinarioId { get; set; }
    public Guid AnimalId { get; set; }
    public Guid TutorId { get; set; }
    public bool DiagnosticoValidado { get; set; }
    public bool ProtocoloValidado { get; set; }
    public StatusPagamento StatusPagamento { get; set; }
    public bool Cancelada { get; set; }
}
