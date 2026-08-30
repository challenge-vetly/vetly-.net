using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Tutor;

/// <summary>
/// Estado de uma finalidade de consentimento, como o Responsável a enxerga no app
/// (RN-061): se está concedida, desde quando, e quando foi revogada pela última vez.
/// </summary>
public class ConsentimentoDto
{
    /// <summary>Finalidade do tratamento de dados.</summary>
    public FinalidadeConsentimento Finalidade { get; set; }

    /// <summary>Se a finalidade está autorizada neste momento.</summary>
    public bool Concedido { get; set; }

    /// <summary>Data e hora da última concessão.</summary>
    public DateTime? ConcedidoEm { get; set; }

    /// <summary>Data e hora da última revogação.</summary>
    public DateTime? RevogadoEm { get; set; }

    /// <summary>
    /// Explicação da finalidade, para a tela de consentimento apresentar de forma
    /// clara e acessível o que está sendo autorizado (RN-060).
    /// </summary>
    public string Descricao { get; set; } = string.Empty;
}

/// <summary>
/// Alteração de uma finalidade. O que não vier no corpo permanece como está —
/// consentimento é granular, e um PUT não deve revogar por omissão (RN-061).
/// </summary>
public class AlterarConsentimentoDto
{
    [Required(ErrorMessage = "A finalidade é obrigatória.")]
    public FinalidadeConsentimento Finalidade { get; set; }

    [Required(ErrorMessage = "Informe se a finalidade está sendo concedida ou revogada.")]
    public bool Concedido { get; set; }
}

/// <summary>Conjunto de alterações de consentimento enviado pelo app.</summary>
public class AtualizarConsentimentosDto
{
    /// <summary>Finalidades a conceder ou revogar. As demais ficam inalteradas.</summary>
    [Required(ErrorMessage = "Informe ao menos uma finalidade.")]
    [MinLength(1, ErrorMessage = "Informe ao menos uma finalidade.")]
    public List<AlterarConsentimentoDto> Consentimentos { get; set; } = [];
}
