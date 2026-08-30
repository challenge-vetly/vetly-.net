using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Animal;

/// <summary>DTO de resposta com os dados de um animal.</summary>
public class AnimalDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Especie { get; set; } = string.Empty;
    public string Raca { get; set; } = string.Empty;
    public DateTime DataNascimento { get; set; }
    public int IdadeEmAnos { get; set; }
    public Guid TutorId { get; set; }
    public List<string> AlertasAtivos { get; set; } = [];

    /// <summary>Peso em quilogramas. Nulo apenas em cadastros anteriores à migration (RN-081).</summary>
    public decimal? PesoKg { get; set; }

    public SexoAnimal? Sexo { get; set; }
    public bool? Castrado { get; set; }
    public Guid? FotoMidiaId { get; set; }
    public List<string> Alergias { get; set; } = [];
    public List<string> CondicoesPreexistentes { get; set; } = [];
    public List<RegistroVacinacaoDto> CarteiraVacinacao { get; set; } = [];

    public bool Ativo { get; set; }
}
