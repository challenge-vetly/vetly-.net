using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>DTO de resposta com os dados de um pagamento.</summary>
public class PagamentoDto
{
    public Guid Id { get; set; }
    public Guid TutorId { get; set; }
    public Guid? ConsultaId { get; set; }
    public Guid? InternacaoId { get; set; }
    public decimal Valor { get; set; }
    public MeioPagamento MeioPagamento { get; set; }
    public DateTime Momento { get; set; }
    public StatusPagamento StatusPagamento { get; set; }
    public decimal PercentualSplit { get; set; }
    public decimal? ValorEstornado { get; set; }
}
