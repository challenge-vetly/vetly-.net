using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Dispositivo;

/// <summary>Registro de um dispositivo do Responsável para receber push (RN-007/RN-092).</summary>
public class RegistrarDispositivoDto
{
    /// <summary>Token de push emitido pelo APNs ou pelo FCM.</summary>
    [Required(ErrorMessage = "O push token é obrigatório.")]
    [MaxLength(255)]
    public string PushToken { get; set; } = string.Empty;

    /// <summary>Plataforma do dispositivo.</summary>
    [Required(ErrorMessage = "A plataforma é obrigatória.")]
    public PlataformaDispositivo Plataforma { get; set; }
}

/// <summary>Dispositivo registrado.</summary>
public class DispositivoDto
{
    public Guid Id { get; set; }
    public Guid TutorId { get; set; }

    /// <summary>Push token, truncado — o valor inteiro não precisa voltar ao cliente.</summary>
    public string PushToken { get; set; } = string.Empty;

    public PlataformaDispositivo Plataforma { get; set; }
    public DateTime RegistradoEm { get; set; }
    public DateTime UltimoUsoEm { get; set; }
    public bool Ativo { get; set; }
}
