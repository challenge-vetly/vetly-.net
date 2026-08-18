using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>DTO de resposta com os dados de uma consulta.</summary>
public class ConsultaDto
{
    public Guid Id { get; set; }
    public DateTime DataHora { get; set; }
    public ModalidadeAtendimento Modalidade { get; set; }
    public TipoServico TipoServico { get; set; }
    public Guid VeterinarioId { get; set; }
    public Guid AnimalId { get; set; }
    public Guid ResponsavelId { get; set; }
    public string? PreSintomas { get; set; }
    public StatusConsulta Status { get; set; }
    public DateTime? LockCheckoutExpiraEm { get; set; }
    public int ContadorRemarcacoes { get; set; }
    public DateTime? DataRealizada { get; set; }
    public bool DiagnosticoValidado { get; set; }
    public bool ProtocoloValidado { get; set; }
}
