using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Prontuario;

/// <summary>DTO de resposta com uma concessão de acesso ao prontuário (RN-083/085).</summary>
public class ConcessaoAcessoProntuarioDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public Guid VeterinarioId { get; set; }
    public Guid ConsultaId { get; set; }
    public BaseAcesso BaseAcesso { get; set; }
    public DateTime ConcedidoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public bool Revogada { get; set; }
}
