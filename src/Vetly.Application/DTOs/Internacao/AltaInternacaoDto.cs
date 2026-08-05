namespace Vetly.Application.DTOs.Internacao;

/// <summary>DTO de resposta para alta de internacao com saldo financeiro apurado.</summary>
public class AltaInternacaoDto
{
    public Guid InternacaoId { get; set; }
    public Guid AnimalId { get; set; }
    public decimal ValorCaucao { get; set; }
    public decimal ValorTotalApurado { get; set; }

    /// <summary>Saldo devedor apos desconto da caucao (negativo = reembolso ao responsavel).</summary>
    public decimal SaldoRestante { get; set; }

    public DateTime DataAlta { get; set; }
}
