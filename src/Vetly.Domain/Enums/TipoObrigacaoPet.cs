namespace Vetly.Domain.Enums;

/// <summary>
/// Natureza de uma obrigação recorrente de cuidado do animal (RN-045/RN-046).
///
/// A categoria importa porque muda a urgência e quem cobra: antirrábica atrasada é
/// questão sanitária, antipulgas atrasado é desconforto. O board ordena por isso.
/// </summary>
public enum TipoObrigacaoPet
{
    /// <summary>Dose ou reforço de vacina.</summary>
    Vacina = 1,

    /// <summary>Vermifugação periódica.</summary>
    Vermifugo = 2,

    /// <summary>Antipulgas, carrapaticida e afins.</summary>
    Antiparasitario = 3,

    /// <summary>Retorno marcado pelo veterinário para acompanhar um quadro.</summary>
    Retorno = 4,

    /// <summary>Check-up periódico, sem queixa associada.</summary>
    CheckUp = 5,

    /// <summary>Medicação de uso contínuo que precisa ser renovada.</summary>
    MedicacaoContinua = 6,

    /// <summary>Exame de acompanhamento com repetição prevista.</summary>
    Exame = 7
}

/// <summary>
/// Situação de uma obrigação em relação a hoje (RN-045).
///
/// Existe <c>Vencendo</c> separado de <c>EmDia</c> porque avisar só no vencimento é
/// avisar tarde: agendar consulta leva dias.
/// </summary>
public enum SituacaoObrigacao
{
    /// <summary>Vence além da janela de aviso.</summary>
    EmDia = 1,

    /// <summary>Vence dentro dos próximos 30 dias.</summary>
    Vencendo = 2,

    /// <summary>Já passou do vencimento.</summary>
    Vencida = 3,

    /// <summary>Fora do board, preservada no histórico.</summary>
    Arquivada = 4
}
