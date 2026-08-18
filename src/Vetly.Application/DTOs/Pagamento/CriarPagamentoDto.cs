using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>DTO de entrada para registro de um novo pagamento.</summary>
public class CriarPagamentoDto
{
    [Required]
    public Guid ResponsavelId { get; set; }

    public Guid? ConsultaId { get; set; }
    public Guid? InternacaoId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "O valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    [Required]
    public MeioPagamento MeioPagamento { get; set; }
}
