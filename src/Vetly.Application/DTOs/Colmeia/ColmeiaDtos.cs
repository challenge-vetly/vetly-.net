using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Colmeia;

/// <summary>
/// Autorização do Responsável para um veterinário alcançar o histórico do animal
/// (RN-090).
/// </summary>
public class ConcederAcessoDto
{
    [Required(ErrorMessage = "O animal é obrigatório.")]
    public Guid AnimalId { get; set; }

    [Required(ErrorMessage = "O veterinário é obrigatório.")]
    public Guid VeterinarioId { get; set; }

    /// <summary>
    /// Até onde a autorização vai. "Compartilhar o histórico" quase nunca quer dizer
    /// tudo — pedir segunda opinião sobre um exame não é abrir o prontuário inteiro.
    /// </summary>
    [Required(ErrorMessage = "O escopo é obrigatório.")]
    public EscopoAcessoColmeia Escopo { get; set; }

    /// <summary>
    /// Validade em dias. Omitido, vale o padrão de 30 dias — acesso clínico que não
    /// expira sozinho é acesso que ninguém lembra de revogar.
    /// </summary>
    [Range(1, 365, ErrorMessage = "A validade deve estar entre 1 e 365 dias.")]
    public int? ValidadeEmDias { get; set; }

    /// <summary>Por que está concedendo — segunda opinião, mudança de clínica, viagem.</summary>
    [MaxLength(300, ErrorMessage = "O motivo deve ter no máximo 300 caracteres.")]
    public string? Motivo { get; set; }
}

/// <summary>Uma autorização da colmeia (RN-090).</summary>
public class AcessoColmeiaDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public Guid TutorId { get; set; }
    public Guid VeterinarioId { get; set; }
    public Guid? EmpresaId { get; set; }
    public EscopoAcessoColmeia Escopo { get; set; }
    public DateTime ConcedidoEm { get; set; }
    public DateTime ExpiraEm { get; set; }
    public DateTime? RevogadoEm { get; set; }
    public string? Motivo { get; set; }

    /// <summary>Se a autorização vale agora — nem revogada, nem vencida.</summary>
    public bool Vigente { get; set; }
}

/// <summary>Um acesso efetivamente feito pela colmeia (RN-090).</summary>
public class LogAcessoColmeiaDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public Guid? VeterinarioId { get; set; }
    public EscopoAcessoColmeia Escopo { get; set; }
    public string? Rota { get; set; }

    /// <summary>Falso quando o acesso foi recusado — tentativa negada também fica.</summary>
    public bool Permitido { get; set; }

    public DateTime OcorridoEm { get; set; }
}
