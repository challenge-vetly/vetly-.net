using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Animal;

/// <summary>DTO de resposta com os dados de um animal.</summary>
public class AnimalDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Especie { get; set; } = string.Empty;
    public string Raca { get; set; } = string.Empty;
    public SexoAnimal Sexo { get; set; }
    public DateTime DataNascimento { get; set; }
    public int IdadeEmAnos { get; set; }
    public Guid ResponsavelId { get; set; }
    public List<string> AlertasAtivos { get; set; } = [];
    public bool Ativo { get; set; }
    public decimal? PesoKg { get; set; }
    public bool Castrado { get; set; }
    public List<string> CondicoesPreExistentes { get; set; } = [];
    public List<string> Alergias { get; set; } = [];
    public List<string> CarteiraVacinacao { get; set; } = [];
    public List<string> MedicacoesEmUso { get; set; } = [];
    public string? FotoUrl { get; set; }
}
