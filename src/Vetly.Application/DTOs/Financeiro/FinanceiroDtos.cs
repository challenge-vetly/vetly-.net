using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Financeiro;

/// <summary>
/// Consolidado financeiro da plataforma num período (RN-070/RN-071/RN-072).
///
/// A conta que este painel precisa fechar é uma só: <c>bruto = comissão + repasse +
/// desconto</c>. Se os três não somam o bruto, há dinheiro sem dono, e é isso que a
/// conferência procura.
/// </summary>
public class ConsolidadoFinanceiroDto
{
    public DateTime PeriodoInicio { get; set; }
    public DateTime PeriodoFim { get; set; }

    /// <summary>Cobranças confirmadas no período.</summary>
    public int TotalDeTransacoes { get; set; }

    /// <summary>Soma dos valores brutos — o preço dos serviços.</summary>
    public decimal ValorBruto { get; set; }

    /// <summary>Retido pela plataforma, já descontado o que a fidelidade consumiu (RN-070).</summary>
    public decimal ComissaoLiquida { get; set; }

    /// <summary>Descontos concedidos por resgate de pontos, custeados pela comissão (RN-051).</summary>
    public decimal DescontosDeFidelidade { get; set; }

    /// <summary>Total devido aos prestadores (RN-072).</summary>
    public decimal RepasseTotal { get; set; }

    /// <summary>Parte do repasse já marcada como paga.</summary>
    public decimal RepasseLiquidado { get; set; }

    /// <summary>Parte do repasse ainda em aberto — a fila de pagamento.</summary>
    public decimal RepassePendente { get; set; }

    /// <summary>
    /// Verdadeiro quando <c>comissão + repasse + desconto</c> fecha o bruto. Falso é
    /// sinal de que alguma transação foi gravada com split incoerente.
    /// </summary>
    public bool Fecha { get; set; }

    /// <summary>Quanto cabe a cada destinatário de repasse (RN-072).</summary>
    public List<RepassePorDestinatarioDto> PorDestinatario { get; set; } = [];
}

/// <summary>Repasse consolidado de um destinatário — veterinário autônomo ou clínica (RN-072).</summary>
public class RepassePorDestinatarioDto
{
    public Guid DestinatarioId { get; set; }

    /// <summary>Nome do prestador, quando encontrado.</summary>
    public string? Nome { get; set; }

    public int TotalDeAtendimentos { get; set; }
    public decimal RepasseTotal { get; set; }
    public decimal RepasseLiquidado { get; set; }

    /// <summary>O que falta pagar a este destinatário.</summary>
    public decimal RepassePendente { get; set; }
}

/// <summary>Pedido de liquidação em lote (RN-071/RN-072).</summary>
public class LiquidarRepasseDto
{
    /// <summary>
    /// Destinatário a liquidar. Nulo liquida todos do período — o fechamento do mês.
    /// </summary>
    public Guid? DestinatarioId { get; set; }

    /// <summary>Início do período a liquidar.</summary>
    [Required(ErrorMessage = "O início do período é obrigatório.")]
    public DateTime Inicio { get; set; }

    /// <summary>Fim do período a liquidar.</summary>
    [Required(ErrorMessage = "O fim do período é obrigatório.")]
    public DateTime Fim { get; set; }

    /// <summary>
    /// Referência do pagamento efetuado fora da plataforma — número da transferência,
    /// lote do banco. Obrigatório: marcar como pago sem dizer com base em quê deixa a
    /// conferência sem âncora.
    /// </summary>
    [Required(ErrorMessage = "A referência da liquidação é obrigatória.")]
    [MaxLength(100, ErrorMessage = "A referência deve ter no máximo 100 caracteres.")]
    public string Referencia { get; set; } = string.Empty;
}

/// <summary>Resultado da liquidação em lote (RN-071).</summary>
public class LiquidacaoRealizadaDto
{
    public DateTime PeriodoInicio { get; set; }
    public DateTime PeriodoFim { get; set; }
    public Guid? DestinatarioId { get; set; }
    public string Referencia { get; set; } = string.Empty;

    /// <summary>Quantos pagamentos foram marcados como liquidados nesta chamada.</summary>
    public int PagamentosLiquidados { get; set; }

    /// <summary>Soma repassada.</summary>
    public decimal ValorLiquidado { get; set; }

    /// <summary>
    /// Pagamentos que já estavam liquidados e foram ignorados. Chamar duas vezes o
    /// mesmo fechamento não paga duas vezes.
    /// </summary>
    public int JaEstavamLiquidados { get; set; }

    public DateTime RealizadaEm { get; set; }
}
