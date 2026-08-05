namespace Vetly.Domain.Enums;

/// <summary>
/// Tipo de obrigação do calendário de cuidado do pet (RN-069). Independente de
/// <see cref="TipoLembrete"/> — decisão de fundação da Fase 0: não reaproveita
/// <c>LembreteAgendado</c> (feature v1 intocada).
/// </summary>
public enum TipoObrigacao
{
    Vacina = 1,
    Vermifugo = 2,
    Retorno = 3,
    CheckUp = 4
}
