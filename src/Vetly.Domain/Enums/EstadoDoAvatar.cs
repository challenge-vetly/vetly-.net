namespace Vetly.Domain.Enums;

/// <summary>
/// Estado visual do avatar do pet, derivado das obrigações (RN-020/RN-096/RN-097,
/// RN-112/RN-113).
///
/// <para>
/// <b>Reversão da decisão anterior.</b> Até a Sprint 3 este era o único dado do avatar
/// que a API produzia, e a documentação dizia que criar a rota <c>/avatar</c> seria
/// regressão de escopo. O app, porém, passou a simular no front saúde, humor e pelo
/// com valores fixos — mock na tela principal do Responsável. A Sprint 4 trouxe o
/// cálculo para o servidor (<c>CondicaoDoAvatar</c>, <c>GET /api/animais/{id}/avatar</c>):
/// a regra fica num lugar só, testável, e o histórico diário passa a existir. Sprite e
/// animação continuam assets do app (RN-096).
/// </para>
/// <para>
/// Os valores novos entram no fim, sem renumerar: o enum é gravado como número no
/// histórico e trafega como texto no JSON. O board mantém em <c>avatarEstado</c> só
/// os três valores originais, para o app publicado não receber um texto que não
/// conhece; o estado completo vem em <c>avatar.estado</c>.
/// </para>
/// </summary>
public enum EstadoDoAvatar
{
    /// <summary>Nenhuma obrigação vencida.</summary>
    Saudavel = 1,

    /// <summary>Vacina em atraso — o estado adoentado da RN-097.</summary>
    VacinaAtrasada = 2,

    /// <summary>Higienização em atraso — o pelo longo da RN-097.</summary>
    HigieneAtrasada = 3,

    /// <summary>Saúde abaixo de 60 sem vacina ou higiene em atraso (RN-113).</summary>
    Abatido = 4,

    /// <summary>Saúde e humor a partir de 90 — tudo em dia e cuidado recente (RN-113).</summary>
    Feliz = 5
}
