using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Notificacao;

/// <summary>Cria uma notificação para o Responsável (RN-092).</summary>
public class CriarNotificacaoDto
{
    [Required(ErrorMessage = "O destinatário é obrigatório.")]
    public Guid TutorId { get; set; }

    [Required(ErrorMessage = "O tipo é obrigatório.")]
    public TipoNotificacao Tipo { get; set; }

    [Required(ErrorMessage = "O título é obrigatório.")]
    [MaxLength(120, ErrorMessage = "O título deve ter no máximo 120 caracteres.")]
    public string Titulo { get; set; } = string.Empty;

    [Required(ErrorMessage = "O corpo é obrigatório.")]
    [MaxLength(500, ErrorMessage = "O corpo deve ter no máximo 500 caracteres.")]
    public string Corpo { get; set; } = string.Empty;

    /// <summary>Quando enviar. Omitido, vale agora.</summary>
    public DateTime? AgendadaPara { get; set; }

    public Guid? AnimalId { get; set; }
    public Guid? ConsultaId { get; set; }

    /// <summary>
    /// Rota interna que o app abre ao ser tocada — o destino é do app, não da API.
    /// </summary>
    [MaxLength(200)]
    public string? Destino { get; set; }
}

/// <summary>Uma notificação na caixa de entrada do Responsável (RN-092).</summary>
public class NotificacaoDto
{
    public Guid Id { get; set; }
    public Guid TutorId { get; set; }
    public TipoNotificacao Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Corpo { get; set; } = string.Empty;

    /// <summary>
    /// <c>NaoEntregue</c> não significa perdida: a notificação segue visível aqui,
    /// porque push perdido não pode significar aviso perdido.
    /// </summary>
    public StatusNotificacao Status { get; set; }

    public Guid? AnimalId { get; set; }
    public Guid? ConsultaId { get; set; }
    public string? Destino { get; set; }

    public DateTime AgendadaPara { get; set; }
    public DateTime? EnviadaEm { get; set; }
    public DateTime? LidaEm { get; set; }
    public DateTime CriadaEm { get; set; }
}
