namespace Vetly.Domain.Enums;

/// <summary>
/// Finalidades de tratamento de dados que o Responsável autoriza separadamente (RN-061).
/// O consentimento é granular por finalidade e revogável a qualquer momento (RN-062).
/// </summary>
public enum FinalidadeConsentimento
{
    /// <summary>
    /// Atendimento clínico. Sem esta finalidade o Responsável não opera nas rotas
    /// de negócio — base legal precede o tratamento (RN-060).
    /// </summary>
    Atendimento = 1,

    /// <summary>Lembretes e comunicação proativa (RN-094).</summary>
    Lembretes = 2,

    /// <summary>
    /// Compartilhamento com clínicas parceiras da rede. É a chave que abre a
    /// colmeia; sem ela vale o acesso restrito (RN-064/RN-066).
    /// </summary>
    Compartilhamento = 3,

    /// <summary>Promoções — exige opt-in específico de marketing (RN-093).</summary>
    Promocoes = 4,

    /// <summary>
    /// Uso em dados agregados e anonimizados (RN-075). Tem opt-out específico
    /// sem perda de funcionalidade no app (RN-077).
    /// </summary>
    DadosAgregados = 5
}
