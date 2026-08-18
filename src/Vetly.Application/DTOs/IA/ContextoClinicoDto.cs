namespace Vetly.Application.DTOs.IA;

/// <summary>DTO com o contexto clínico enviado ao Ollama para geração de hipóteses diagnósticas.</summary>
public class ContextoClinicoDto
{
    /// <summary>Espécie do animal (ex: Canino, Felino).</summary>
    public string Especie { get; set; } = string.Empty;

    /// <summary>Raça do animal.</summary>
    public string Raca { get; set; } = string.Empty;

    /// <summary>Idade do animal em anos.</summary>
    public int IdadeAnos { get; set; }

    /// <summary>Peso do animal em quilogramas.</summary>
    public decimal PesoKg { get; set; }

    /// <summary>Sintomas relatados pelo responsavel ou observados pelo veterinário.</summary>
    public List<string> Sintomas { get; set; } = [];

    /// <summary>Histórico clínico relevante (ex: cirurgias, doenças crônicas).</summary>
    public string? HistoricoRelevante { get; set; }
}
