using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Veterinario;

/// <summary>
/// Situação do CRMV de um veterinário e o reflexo dela no matching (RN-107).
/// </summary>
public class SituacaoCrmvDto
{
    public Guid VeterinarioId { get; set; }

    /// <summary>Registro no formato XXXXXX-UF.</summary>
    public string Crmv { get; set; } = string.Empty;

    public string UfAtuacao { get; set; } = string.Empty;

    /// <summary>Última resposta conhecida do conselho.</summary>
    public StatusCrmv Status { get; set; }

    /// <summary>Data/hora da última consulta ao conselho.</summary>
    public DateTime? ValidadoEm { get; set; }

    /// <summary>Perfil publicado no matching — só acontece com status <c>Valido</c>.</summary>
    public bool Publicado { get; set; }

    public DateTime? PublicadoEm { get; set; }
}
