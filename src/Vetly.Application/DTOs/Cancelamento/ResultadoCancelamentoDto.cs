namespace Vetly.Application.DTOs.Cancelamento;

/// <summary>
/// Resultado do processamento de cancelamento de uma consulta.
/// Produzido pelas implementações de <c>ICancelamentoStrategy</c>.
/// </summary>
public class ResultadoCancelamentoDto
{
    /// <summary>Valor a ser devolvido ao responsavel.</summary>
    public decimal ValorReembolso { get; set; }

    /// <summary>Percentual do valor original que foi retido pela plataforma.</summary>
    public decimal PercentualRetencao { get; set; }

    /// <summary>Descrição legível da política de reembolso aplicada.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Nome da estratégia aplicada (ex: "Reembolso Integral").</summary>
    public string EstrategiaAplicada { get; set; } = string.Empty;

    /// <summary>Janela de antecedência do cancelamento: ">24h", "24h-2h" ou "&lt;2h" (RN-062/063).</summary>
    public string Janela { get; set; } = string.Empty;

    /// <summary>Sempre false no MVP — o reembolso é calculado e registrado, nunca liquidado (RN-037/062).</summary>
    public bool Liquidado { get; set; }
}
