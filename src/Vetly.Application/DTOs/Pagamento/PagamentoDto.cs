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
    /// <summary>Percentual que fica com o prestador. Mantido por compatibilidade.</summary>
    public decimal PercentualSplit { get; set; }

    /// <summary>Plano que definiu o take rate (RN-070).</summary>
    public PlanoAssinatura? PlanoAplicado { get; set; }

    /// <summary>Percentual retido pela Vetly (RN-070).</summary>
    public decimal? TakeRate { get; set; }

    /// <summary>Comissão da Vetly — registrada, não liquidada (RN-071).</summary>
    public decimal? Comissao { get; set; }

    /// <summary>Repasse ao prestador — registrado, não liquidado (RN-071).</summary>
    public decimal? Repasse { get; set; }

    /// <summary>Quem recebe o repasse: o veterinário autônomo ou a empresa (RN-072).</summary>
    public Guid? DestinatarioRepasseId { get; set; }
    public decimal? ValorEstornado { get; set; }
}
