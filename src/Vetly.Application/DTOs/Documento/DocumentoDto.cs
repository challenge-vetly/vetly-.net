using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Documento;

/// <summary>DTO de resposta com os dados de um documento clínico.</summary>
public class DocumentoDto
{
    public Guid Id { get; set; }
    public Guid? ConsultaId { get; set; }
    public Guid? InternacaoId { get; set; }
    public TipoDocumento TipoDocumento { get; set; }
    public int Versao { get; set; }
    public DateTime DataGeracao { get; set; }
    public string CrmvSignatario { get; set; } = string.Empty;
    public bool AssinadoDigitalmente { get; set; }
    public Guid? VersaoOriginalId { get; set; }
    public DateTime? DataCorrecao { get; set; }
    public string? CrmvSolicitanteCorrecao { get; set; }
}
