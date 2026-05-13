using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Prontuario;

/// <summary>DTO de entrada para correção de um prontuário existente (RN-032/033/034).</summary>
public class CorrigirProntuarioDto
{
    [Required(ErrorMessage = "Os novos dados clínicos são obrigatórios.")]
    public string NovosDadosClinicos { get; set; } = string.Empty;

    /// <summary>
    /// Obrigatório quando a correção ocorre após 24h do registro original (RN-033).
    /// O serviço valida esta regra antes de aceitar a correção.
    /// </summary>
    [MaxLength(1000)]
    public string? Justificativa { get; set; }

    [Required(ErrorMessage = "O CRMV do solicitante é obrigatório.")]
    public string CrmvSolicitante { get; set; } = string.Empty;
}
