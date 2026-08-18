using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>DTO de resposta da simulação de pagamento (RN-037/089), com o novo status da consulta.</summary>
public class SimularPagamentoResponseDto
{
    public Guid Id { get; set; }
    public StatusPagamento Status { get; set; }
    public bool Simulado { get; set; }
    public decimal PercentualComissao { get; set; }
    public decimal ValorComissao { get; set; }
    public decimal ValorRepasse { get; set; }
    public StatusConsulta ConsultaStatus { get; set; }

    /// <summary>Desconto de fidelidade calculado e exibido, sem abatimento real (RN-072).</summary>
    public decimal DescontoFidelidadeCalculado { get; set; }
    public decimal IncidenciaVetly { get; set; }
    public decimal IncidenciaVeterinario { get; set; }
}
