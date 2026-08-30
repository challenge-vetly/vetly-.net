using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>DTO de entrada para agendamento de uma nova consulta.</summary>
public class CriarConsultaDto
{
    [Required(ErrorMessage = "A data e hora são obrigatórias.")]
    public DateTime DataHora { get; set; }

    [Required]
    public ModalidadeAtendimento Modalidade { get; set; }

    [Required(ErrorMessage = "O veterinário é obrigatório.")]
    public Guid VeterinarioId { get; set; }

    [Required(ErrorMessage = "O animal é obrigatório.")]
    public Guid AnimalId { get; set; }

    [Required(ErrorMessage = "O tutor é obrigatório.")]
    public Guid TutorId { get; set; }

    /// <summary>
    /// Id do pagamento já processado.
    /// Obrigatório pois a consulta só é confirmada após pagamento (RN-006).
    /// </summary>
    [Required(ErrorMessage = "O id do pagamento confirmado é obrigatório (RN-006).")]
    public Guid PagamentoId { get; set; }
}
