using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>DTO de entrada para remarcar uma consulta (RN-022).</summary>
public class RemarcarConsultaDto
{
    [Required(ErrorMessage = "A nova data e hora são obrigatórias.")]
    public DateTime NovaDataHora { get; set; }
}
