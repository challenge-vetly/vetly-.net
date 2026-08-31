using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>DTO de entrada para registro de um novo pagamento.</summary>
public class CriarPagamentoDto
{
    [Required]
    public Guid TutorId { get; set; }

    public Guid? ConsultaId { get; set; }
    public Guid? InternacaoId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "O valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    [Required]
    public MeioPagamento MeioPagamento { get; set; }

    /// <summary>
    /// Cupom de fidelidade a aplicar nesta cobrança (RN-051/RN-053).
    ///
    /// O cupom é emitido antes, em <c>POST /api/fidelidade/resgates</c>: os pontos já
    /// saíram do saldo e a divisão do custo já está gravada nele. Aqui ele só é
    /// consumido — assim o Responsável vê o desconto antes de decidir pagar.
    /// </summary>
    public Guid? CupomId { get; set; }
}
