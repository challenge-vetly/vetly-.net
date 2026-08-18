using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>DTO de resposta com os dados de um pagamento.</summary>
public class PagamentoDto
{
    public Guid Id { get; set; }
    public Guid ResponsavelId { get; set; }
    public Guid? ConsultaId { get; set; }
    public Guid? InternacaoId { get; set; }
    public decimal Valor { get; set; }
    public MeioPagamento MeioPagamento { get; set; }
    public DateTime Momento { get; set; }
    public StatusPagamento StatusPagamento { get; set; }
    public decimal PercentualSplit { get; set; }
    public decimal? ValorEstornado { get; set; }
    public bool Simulado { get; set; }
    public decimal PercentualComissao { get; set; }
    public decimal ValorComissao { get; set; }
    public decimal ValorRepasse { get; set; }
    public decimal DescontoFidelidadeCalculado { get; set; }
    public decimal IncidenciaVetly { get; set; }
    public decimal IncidenciaVeterinario { get; set; }
}
