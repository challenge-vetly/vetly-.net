using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Prontuario;

/// <summary>DTO de resposta com um registro do log de acesso ao prontuário (RN-086).</summary>
public class LogAcessoProntuarioDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public Guid VeterinarioId { get; set; }
    public DateTime DataHora { get; set; }
    public string Contexto { get; set; } = string.Empty;
    public BaseAcesso BaseAcesso { get; set; }
}
