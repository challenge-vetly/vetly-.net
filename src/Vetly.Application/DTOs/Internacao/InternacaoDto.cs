namespace Vetly.Application.DTOs.Internacao;

/// <summary>DTO de resposta com os dados de uma internação.</summary>
public class InternacaoDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public Guid VeterinarioId { get; set; }
    public decimal ValorCaucao { get; set; }
    public decimal ValorTotalApurado { get; set; }
    public DateTime DataAbertura { get; set; }
    public DateTime? DataAlta { get; set; }
    public bool EstaAtiva { get; set; }
    public int DiasInternado { get; set; }
    public string ProcedimentosDiarios { get; set; } = string.Empty;
}
