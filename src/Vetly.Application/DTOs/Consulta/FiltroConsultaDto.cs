using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>
/// Filtros da listagem de consultas (§3.5 do documento de engenharia).
/// </summary>
public class FiltroConsultaDto
{
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public Guid? VeterinarioId { get; set; }

    /// <summary>Filtra pelas consultas de um Responsável.</summary>
    public Guid? TutorId { get; set; }

    /// <summary>Filtra pelas consultas de um animal.</summary>
    public Guid? AnimalId { get; set; }

    /// <summary>Filtra pelo estado da consulta (RN-035/RN-038).</summary>
    public StatusConsulta? Status { get; set; }

    /// <summary>
    /// Mantido por compatibilidade enquanto dura a dupla escrita do
    /// <c>StatusConsulta</c>. Prefira <see cref="Status"/>.
    /// </summary>
    public bool? Cancelada { get; set; }
}
