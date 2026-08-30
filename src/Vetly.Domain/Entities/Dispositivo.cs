using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Dispositivo do Responsável registrado para receber push (RN-007/RN-092).
/// Um Responsável tem N dispositivos; o mesmo push token não se repete entre eles.
/// </summary>
public class Dispositivo
{
    /// <summary>Identificador único do dispositivo (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id do Responsável dono do dispositivo.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>Token de push emitido pelo APNs ou pelo FCM.</summary>
    [Required]
    [MaxLength(255)]
    public string PushToken { get; private set; }

    /// <summary>Plataforma do dispositivo.</summary>
    [Required]
    public PlataformaDispositivo Plataforma { get; private set; }

    /// <summary>Data e hora do registro (UTC).</summary>
    public DateTime RegistradoEm { get; private set; }

    /// <summary>Último momento em que o dispositivo foi visto ativo.</summary>
    public DateTime UltimoUsoEm { get; private set; }

    /// <summary>
    /// Indica se o dispositivo segue ativo. A remoção é lógica: o histórico de
    /// entrega de push depende do registro.
    /// </summary>
    public bool Ativo { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Dispositivo() => PushToken = null!;

    /// <summary>Registra um dispositivo do Responsável para push.</summary>
    public Dispositivo(Guid tutorId, string pushToken, PlataformaDispositivo plataforma)
    {
        if (string.IsNullOrWhiteSpace(pushToken))
            throw new ArgumentException("O push token é obrigatório.", nameof(pushToken));

        Id = Guid.NewGuid();
        TutorId = tutorId;
        PushToken = pushToken;
        Plataforma = plataforma;
        RegistradoEm = DateTime.UtcNow;
        UltimoUsoEm = RegistradoEm;
        Ativo = true;
    }

    /// <summary>Reativa um registro existente e atualiza o último uso.</summary>
    public void Reativar(DateTime quando)
    {
        Ativo = true;
        UltimoUsoEm = quando;
    }

    /// <summary>Desativa o dispositivo — o app foi desinstalado ou o token expirou.</summary>
    public void Desativar() => Ativo = false;
}
