using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Obrigacao;

/// <summary>Cria uma obrigação recorrente de cuidado do animal (RN-045).</summary>
public class CriarObrigacaoDto
{
    [Required(ErrorMessage = "O tipo da obrigação é obrigatório.")]
    public TipoObrigacaoPet Tipo { get; set; }

    /// <summary>O que precisa ser feito: "V10", "Antirrábica", "Vermífugo".</summary>
    [Required(ErrorMessage = "A descrição é obrigatória.")]
    [MaxLength(120, ErrorMessage = "A descrição deve ter no máximo 120 caracteres.")]
    public string Descricao { get; set; } = string.Empty;

    [Required(ErrorMessage = "O vencimento é obrigatório.")]
    public DateTime ProximoVencimento { get; set; }

    /// <summary>
    /// De quantos em quantos dias se repete. Zero para obrigação de uma vez só —
    /// um retorno pontual, por exemplo.
    /// </summary>
    [Range(0, 3650, ErrorMessage = "A periodicidade deve estar entre 0 e 3650 dias.")]
    public int PeriodicidadeEmDias { get; set; }
}

/// <summary>Registra que a obrigação foi cumprida (RN-045).</summary>
public class CumprirObrigacaoDto
{
    /// <summary>Quando foi cumprida. Omitido, vale agora.</summary>
    public DateTime? Quando { get; set; }

    /// <summary>Consulta em que foi cumprida, quando houve uma.</summary>
    public Guid? ConsultaId { get; set; }
}

/// <summary>Uma obrigação no board do pet (RN-045/RN-046).</summary>
public class ObrigacaoPetDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public TipoObrigacaoPet Tipo { get; set; }
    public string Descricao { get; set; } = string.Empty;

    public DateTime ProximoVencimento { get; set; }
    public int PeriodicidadeEmDias { get; set; }

    /// <summary>Situação em relação a hoje.</summary>
    public SituacaoObrigacao Situacao { get; set; }

    /// <summary>Dias até vencer; negativo quando já venceu.</summary>
    public int DiasAteVencer { get; set; }

    public DateTime? UltimoCumprimento { get; set; }
    public Guid? UltimaConsultaId { get; set; }

    /// <summary>Veio da carteira de vacinação, e não de alguém digitando.</summary>
    public bool DerivadaDaCarteira { get; set; }

    public bool Arquivada { get; set; }
}

/// <summary>
/// Board de obrigações de um animal (RN-045/RN-046).
///
/// Traz a contagem por situação junto da lista porque a primeira pergunta do
/// Responsável não é "quais são", é "tem alguma coisa atrasada?".
/// </summary>
public class BoardDeObrigacoesDto
{
    public Guid AnimalId { get; set; }
    public string AnimalNome { get; set; } = string.Empty;

    public int TotalVencidas { get; set; }
    public int TotalVencendo { get; set; }
    public int TotalEmDia { get; set; }

    /// <summary>
    /// Verdadeiro quando há algo vencido. É o que acende o aviso no app, e o que a
    /// clínica usa para priorizar contato (RN-095).
    /// </summary>
    public bool TemPendencia { get; set; }

    /// <summary>Obrigações ativas, das mais urgentes às menos.</summary>
    public List<ObrigacaoPetDto> Obrigacoes { get; set; } = [];
}
