using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>DTO de entrada para simular o pagamento de uma consulta (RN-037).</summary>
public class SimularPagamentoDto
{
    [Required(ErrorMessage = "O id da consulta é obrigatório.")]
    public Guid ConsultaId { get; set; }

    [Required(ErrorMessage = "O valor é obrigatório.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "O valor deve ser maior que zero.")]
    public decimal Valor { get; set; }

    [Required(ErrorMessage = "O meio de pagamento é obrigatório.")]
    public MeioPagamento Meio { get; set; }
}
