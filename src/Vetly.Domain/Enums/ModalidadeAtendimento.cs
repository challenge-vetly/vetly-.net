namespace Vetly.Domain.Enums;

/// <summary>
/// Modalidade do atendimento, registrada no momento do agendamento (RN-039).
/// </summary>
public enum ModalidadeAtendimento
{
    /// <summary>Atendimento presencial — única modalidade aceita no MVP (RN-039).</summary>
    Presencial = 1,

    /// <summary>
    /// Atendimento remoto por videochamada. Alvo de produção: o valor permanece no enum
    /// para não exigir migration futura, mas é rejeitado na validação do agendamento (RN-039).
    /// </summary>
    Remoto = 2
}
