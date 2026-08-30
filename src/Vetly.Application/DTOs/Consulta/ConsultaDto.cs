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
    /// <summary>Estado da consulta na máquina de estados do agendamento (RN-035/RN-038).</summary>
    public StatusConsulta Status { get; set; }

    /// <summary>
    /// Mantido por compatibilidade enquanto dura a dupla escrita — use <see cref="Status"/>.
    /// </summary>
    public bool Cancelada { get; set; }
    public bool Finalizada { get; set; }
}
