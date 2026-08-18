using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>
/// DTO de entrada para agendamento de uma nova consulta.
/// A consulta nasce em EmCheckout (RN-058); o pagamento é confirmado depois, em uma
/// etapa separada (POST /api/pagamentos/simular — Fase 5), não neste payload.
/// </summary>
public class CriarConsultaDto
{
    [Required(ErrorMessage = "A data e hora são obrigatórias.")]
    public DateTime DataHora { get; set; }

    [Required]
    public ModalidadeAtendimento Modalidade { get; set; }

    [Required(ErrorMessage = "O tipo de serviço é obrigatório.")]
    public TipoServico TipoServico { get; set; }

    [Required(ErrorMessage = "O veterinário é obrigatório.")]
    public Guid VeterinarioId { get; set; }

    [Required(ErrorMessage = "O animal é obrigatório.")]
    public Guid AnimalId { get; set; }

    [Required(ErrorMessage = "O responsavel é obrigatório.")]
    public Guid ResponsavelId { get; set; }

    /// <summary>Pré-sintomas relatados no agendamento (texto guiado + mídia — RN-059).</summary>
    [MaxLength(4000)]
    public string? PreSintomas { get; set; }
}
