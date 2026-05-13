namespace Vetly.Application.DTOs.Cancelamento;

/// <summary>
/// Resultado do processamento de cancelamento de uma consulta.
/// Produzido pelas implementações de <c>ICancelamentoStrategy</c>.
/// </summary>
public class ResultadoCancelamentoDto
{
    /// <summary>Valor a ser devolvido ao tutor.</summary>
    public decimal ValorReembolso { get; set; }

    /// <summary>Percentual do valor original que foi retido pela plataforma.</summary>
    public decimal PercentualRetencao { get; set; }

    /// <summary>Descrição legível da política de reembolso aplicada.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>Nome da estratégia aplicada (ex: "Reembolso Integral").</summary>
    public string EstrategiaAplicada { get; set; } = string.Empty;
}
