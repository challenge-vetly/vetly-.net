namespace Vetly.Domain.Enums;

/// <summary>
/// Tipo de serviço agendado. Procedimentos físicos (Vacinacao, Cirurgia, Exame) exigem
/// modalidade presencial (RN-025/057).
/// </summary>
public enum TipoServico
{
    Consulta = 1,
    Retorno = 2,
    Vacinacao = 3,
    Cirurgia = 4,
    Exame = 5,
    Teleorientacao = 6
}
