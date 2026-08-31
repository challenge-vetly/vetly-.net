using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Avaliacao;

/// <summary>Avaliação de um atendimento pelo Responsável (RN-055).</summary>
public class CriarAvaliacaoDto
{
    [Required(ErrorMessage = "A nota é obrigatória.")]
    [Range(1, 5, ErrorMessage = "A nota deve estar entre 1 e 5.")]
    public int Nota { get; set; }

    [MaxLength(1000, ErrorMessage = "O comentário deve ter no máximo 1000 caracteres.")]
    public string? Comentario { get; set; }
}

/// <summary>Resposta pública do veterinário a uma avaliação (RN-055).</summary>
public class ResponderAvaliacaoDto
{
    [Required(ErrorMessage = "A resposta é obrigatória.")]
    [MaxLength(1000, ErrorMessage = "A resposta deve ter no máximo 1000 caracteres.")]
    public string Resposta { get; set; } = string.Empty;
}

/// <summary>Moderação do comentário de uma avaliação.</summary>
public class ModerarAvaliacaoDto
{
    /// <summary>Por que o comentário foi escondido. Obrigatório: moderação sem motivo não se audita.</summary>
    [Required(ErrorMessage = "O motivo da moderação é obrigatório.")]
    [MaxLength(300, ErrorMessage = "O motivo deve ter no máximo 300 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Uma avaliação, na visão pública (RN-055).</summary>
public class AvaliacaoDto
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }
    public Guid VeterinarioId { get; set; }
    public Guid? EmpresaId { get; set; }
    public int Nota { get; set; }

    /// <summary>Nulo quando o comentário foi moderado. A nota continua valendo.</summary>
    public string? Comentario { get; set; }

    public bool ComentarioModerado { get; set; }

    /// <summary>
    /// Falso quando a consulta avaliada foi cancelada ou reembolsada: a avaliação sai
    /// do cálculo da nota, mas continua no histórico (RN-059).
    /// </summary>
    public bool Valida { get; set; }
    public string? RespostaDoVeterinario { get; set; }
    public DateTime? RespondidaEm { get; set; }
    public DateTime CriadaEm { get; set; }
}

/// <summary>
/// Um atendimento esperando avaliação (RN-055).
///
/// O prazo aparece na resposta porque é ele que dá urgência ao aviso: "faltam 3 dias"
/// move mais que "avalie quando puder".
/// </summary>
public class AvaliacaoPendenteDto
{
    public Guid ConsultaId { get; set; }
    public Guid AnimalId { get; set; }
    public Guid VeterinarioId { get; set; }
    public string VeterinarioNome { get; set; } = string.Empty;

    public DateTime DataDoAtendimento { get; set; }

    /// <summary>Até quando a avaliação é aceita — 14 dias após o atendimento.</summary>
    public DateTime PrazoAte { get; set; }

    public int DiasRestantes { get; set; }
}

/// <summary>Reputação de um veterinário (RN-057).</summary>
public class ReputacaoDto
{
    public Guid VeterinarioId { get; set; }
    public decimal NotaMedia { get; set; }
    public int NumAvaliacoes { get; set; }

    /// <summary>
    /// Falso enquanto não há avaliações suficientes: uma nota 5 vinda de uma única
    /// avaliação não diz nada sobre o profissional (RN-057). Abaixo do mínimo, o
    /// matching usa o selo "Novo na Vetly" (RN-033).
    /// </summary>
    public bool NotaPublica { get; set; }

    public int MinimoParaNotaPublica { get; set; }

    /// <summary>Quantas avaliações por nota — de 1 a 5.</summary>
    public Dictionary<int, int> Distribuicao { get; set; } = [];

    public List<AvaliacaoDto> Avaliacoes { get; set; } = [];
}
