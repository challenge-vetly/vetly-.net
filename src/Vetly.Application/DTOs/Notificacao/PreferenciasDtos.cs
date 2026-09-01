namespace Vetly.Application.DTOs.Notificacao;

/// <summary>
/// O que o Responsável escolheu receber (RN-093).
///
/// Só há uma escolha porque só há um tipo opcional. Aviso de consulta, documento
/// publicado e obrigação vencendo são o serviço contratado — oferecê-los como
/// preferência faria o app poder deixar de avisar sobre a saúde do animal.
/// </summary>
public class PreferenciasDeNotificacaoDto
{
    public Guid TutorId { get; set; }

    /// <summary>
    /// Comunicações promocionais. Padrão desligado: opt-in, nunca opt-out.
    /// </summary>
    public bool AceitaPromocoes { get; set; }

    /// <summary>Quando a escolha foi feita pela última vez. Nulo se nunca foi mexida.</summary>
    public DateTime? AtualizadoEm { get; set; }
}

/// <summary>Alteração das preferências de notificação (RN-093).</summary>
public class AtualizarPreferenciasDto
{
    /// <summary>Ligar ou desligar as comunicações promocionais.</summary>
    public bool AceitaPromocoes { get; set; }
}
