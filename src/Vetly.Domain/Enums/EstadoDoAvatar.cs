namespace Vetly.Domain.Enums;

/// <summary>
/// Estado visual do avatar do pet, derivado das obrigações (RN-020/RN-096/RN-097).
///
/// É o único dado do avatar que a API produz. O sprite, a animação e a "reclamação"
/// são assets no bundle do app, e no MVP só há cachorro (RN-096) — não existe
/// <c>TB_AVATAR</c> nem rota <c>/avatar</c>, e criar uma seria regressão de escopo.
///
/// O enum já nasce na forma-alvo da RN-097 para que o mock do front não precise mudar
/// quando o comportamento virar sistema.
/// </summary>
public enum EstadoDoAvatar
{
    /// <summary>Nenhuma obrigação vencida.</summary>
    Saudavel = 1,

    /// <summary>Vacina em atraso — o estado adoentado da RN-097.</summary>
    VacinaAtrasada = 2,

    /// <summary>Higienização em atraso — o pelo longo da RN-097.</summary>
    HigieneAtrasada = 3
}
