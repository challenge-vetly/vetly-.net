using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Fidelidade;

/// <summary>DTO de resposta com um lançamento do extrato de pontos de fidelidade.</summary>
public class PontosFidelidadeDto
{
    public Guid Id { get; set; }
    public Guid ResponsavelId { get; set; }
    public Guid ConsultaId { get; set; }
    public OrigemPontos Origem { get; set; }
    public int Pontos { get; set; }
    public DateTime Data { get; set; }
    public DateTime ExpiraEm { get; set; }
    public bool Estornado { get; set; }
    public bool Valido { get; set; }
}
