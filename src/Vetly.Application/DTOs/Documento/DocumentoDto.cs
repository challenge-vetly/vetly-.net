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

    /// <summary>Conteúdo do documento, formatado a partir do estado final (RN-083).</summary>
    public string? Conteudo { get; set; }

    /// <summary>Id da mídia com o PDF renderizado.</summary>
    public Guid? PdfMidiaId { get; set; }

    /// <summary>Subtipo do atestado (saúde, óbito, transporte). Nulo nos demais tipos (RN-086).</summary>
    public TipoAtestado? Subtipo { get; set; }

    /// <summary>Método da assinatura — nome digitado no MVP (RN-087).</summary>
    public string? AssinaturaMetodo { get; set; }

    public string? AssinaturaCarimbo { get; set; }

    /// <summary>Data de publicação no board do pet (RN-011/RN-090).</summary>
    public DateTime? PublicadoEm { get; set; }

    public DateTime? LidoEm { get; set; }
    public bool AssinadoDigitalmente { get; set; }
    public Guid? VersaoOriginalId { get; set; }
    public DateTime? DataCorrecao { get; set; }
    public string? CrmvSolicitanteCorrecao { get; set; }
}
