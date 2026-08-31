using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Midia;

/// <summary>Pedido de espaço no storage para um arquivo (§2.6).</summary>
public class SolicitarUploadDto
{
    /// <summary>Natureza do arquivo.</summary>
    [Required(ErrorMessage = "O tipo da mídia é obrigatório.")]
    public TipoMidia Tipo { get; set; }

    /// <summary>Tipo MIME do arquivo que será enviado.</summary>
    [Required(ErrorMessage = "O content type é obrigatório.")]
    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Consulta a que o arquivo pertence, quando aplicável.</summary>
    public Guid? ConsultaId { get; set; }
}

/// <summary>Espaço reservado no storage e a URL para enviar o arquivo.</summary>
public class UrlDeUploadDto
{
    /// <summary>Id que viaja nos payloads de negócio. Nunca a URL, que expira.</summary>
    public Guid MidiaId { get; set; }

    /// <summary>Endereço para o app enviar o arquivo, direto ao storage.</summary>
    public string UploadUrl { get; set; } = string.Empty;

    public DateTime ExpiraEm { get; set; }

    public string ContentType { get; set; } = string.Empty;
}

/// <summary>URL temporária para ler um arquivo (RN-090).</summary>
public class UrlDeLeituraDto
{
    public Guid MidiaId { get; set; }
    public string Url { get; set; } = string.Empty;
    public DateTime ExpiraEm { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long? TamanhoBytes { get; set; }
}
