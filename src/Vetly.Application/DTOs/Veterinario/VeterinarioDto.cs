using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Veterinario;

/// <summary>DTO de resposta com os dados públicos de um veterinário.</summary>
public class VeterinarioDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Crmv { get; set; } = string.Empty;
    public string UfAtuacao { get; set; } = string.Empty;
    public List<string> Especialidades { get; set; } = [];
    public List<string> EspeciesAtendidas { get; set; } = [];
    public string? TitulacaoAcademica { get; set; }
    public PersonaVeterinario Persona { get; set; }
    public PlanoAssinatura Plano { get; set; }
    public bool Ativo { get; set; }
    public Guid? EmpresaId { get; set; }
    public int StrikesAtivos { get; set; }
    public DateTime? SuspensoAte { get; set; }

    /// <summary>
    /// Média ponderada por recência (RN-078). Nula quando <see cref="TotalAvaliacoes"/>
    /// é menor que 3 — a nota só é exibida publicamente a partir da terceira avaliação.
    /// </summary>
    public decimal? NotaMedia { get; set; }

    /// <summary>Total de avaliações não invalidadas recebidas (RN-078).</summary>
    public int TotalAvaliacoes { get; set; }
}
