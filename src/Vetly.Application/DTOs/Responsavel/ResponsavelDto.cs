using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Responsavel;

/// <summary>DTO de resposta com os dados de um responsavel.</summary>
public class ResponsavelDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public bool ConsentimentoAtendimento { get; set; }
    public bool ConsentimentoLembretes { get; set; }
    public bool ConsentimentoCompartilhamento { get; set; }
    public DateTime? DataConsentimento { get; set; }
    public bool Ativo { get; set; }
    public TierFidelidade TierFidelidade { get; set; }
    public int SaldoPontos { get; set; }
    public decimal SaldoCreditosVetly { get; set; }
    public int NoShowsAtivos { get; set; }
    public DateTime? BloqueadoDescontosAte { get; set; }
}
