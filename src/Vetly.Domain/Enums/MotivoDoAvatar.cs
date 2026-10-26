namespace Vetly.Domain.Enums;

/// <summary>
/// Por que o avatar está como está (RN-112). Cada motivo corresponde a uma linha da
/// tabela de pesos de <c>CondicaoDoAvatar</c>, e é o que o app usa para explicar a
/// queda de saúde ou de humor — um número sem motivo não diz ao Responsável o que
/// fazer.
/// </summary>
public enum MotivoDoAvatar
{
    /// <summary>Vacina vencida.</summary>
    VacinaVencida = 1,

    /// <summary>Vermífugo ou antiparasitário vencido.</summary>
    VermifugoVencido = 2,

    /// <summary>Banho e tosa vencidos.</summary>
    HigieneVencida = 3,

    /// <summary>Check-up periódico vencido.</summary>
    CheckUpVencido = 4,

    /// <summary>Nenhuma consulta realizada nos últimos 12 meses.</summary>
    SemConsultaHa12Meses = 5,

    /// <summary>Peso não registrado nos últimos 6 meses.</summary>
    PesoDesatualizado = 6,

    /// <summary>
    /// Retorno, medicação contínua ou exame de acompanhamento vencido. A tabela de
    /// pesos penaliza esses três; sem um motivo próprio, a saúde cairia sem explicação.
    /// </summary>
    AcompanhamentoVencido = 7
}
