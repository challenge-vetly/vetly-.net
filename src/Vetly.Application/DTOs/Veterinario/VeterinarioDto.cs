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
}
