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

    /// <summary>Endereço do veterinário, com a coordenada derivada (RN-026).</summary>
    public EnderecoDto? Endereco { get; set; }

    // ── Matching e reputação (somente leitura) ───────────────────────────────

    /// <summary>Resultado da validação do CRMV junto ao conselho (RN-107).</summary>
    public StatusCrmv CrmvStatus { get; set; }
    public DateTime? CrmvValidadoEm { get; set; }

    /// <summary>Nota média. Só vale publicamente a partir de 3 avaliações (RN-057).</summary>
    public decimal NotaMedia { get; set; }
    public int NumAvaliacoes { get; set; }

    /// <summary>Verdadeiro quando a nota já pode ser exibida e entrar no score (RN-057).</summary>
    public bool NotaPublica { get; set; }

    public StatusMatching MatchingStatus { get; set; }

    /// <summary>Perfil publicado no matching. Exige CRMV válido (RN-107).</summary>
    public bool Publicado { get; set; }
    public DateTime? PublicadoEm { get; set; }
    public Guid? EmpresaId { get; set; }
}
