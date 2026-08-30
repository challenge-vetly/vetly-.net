namespace Vetly.Domain.Enums;

/// <summary>
/// Resultado da validação do CRMV junto ao conselho regional (RN-107).
/// O formato do registro é validado pelo value object <c>Crmv</c>; este enum guarda
/// o que o conselho respondeu. Perfil que não esteja <see cref="Valido"/> não é
/// publicado no matching — nunca se aprova por omissão.
/// </summary>
public enum StatusCrmv
{
    /// <summary>Ainda não validado, ou conselho indisponível na última tentativa (RN-107).</summary>
    PendenteValidacao = 1,

    /// <summary>Registro válido e ativo no conselho — única condição que libera a publicação.</summary>
    Valido = 2,

    /// <summary>Registro inválido segundo o conselho.</summary>
    Invalido = 3,

    /// <summary>Registro suspenso segundo o conselho.</summary>
    Suspenso = 4
}
