namespace Vetly.Application.DTOs.Exame;

/// <summary>DTO de resposta com os dados de um exame.</summary>
public class ExameDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public Guid VeterinarioId { get; set; }
    public string TipoSolicitacao { get; set; } = string.Empty;
    public string? Resultado { get; set; }

    /// <summary>Mídias do laudo — PDF e imagens (RN-104).</summary>
    public List<Guid> MidiaIds { get; set; } = [];
    public bool LiberadoAoTutor { get; set; }
    public DateTime DataSolicitacao { get; set; }
    public DateTime? DataResultado { get; set; }
}
