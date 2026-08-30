namespace Vetly.Domain.Enums;

/// <summary>Plataforma do dispositivo que recebe push (RN-007/RN-092).</summary>
public enum PlataformaDispositivo
{
    /// <summary>iOS — push via APNs.</summary>
    Ios = 1,

    /// <summary>Android — push via FCM.</summary>
    Android = 2
}
