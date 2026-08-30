namespace Vetly.Domain.Enums;

/// <summary>
/// Necessidade atendida pelo prestador (RN-002, RN-032).
/// É o filtro que o Responsável usa na busca.
/// </summary>
public enum TipoServico
{
    /// <summary>Consulta de rotina.</summary>
    ConsultaRotina = 1,

    /// <summary>Consulta de emergência.</summary>
    Emergencia = 2,

    /// <summary>Vacinação.</summary>
    Vacinacao = 3,

    /// <summary>Exame clínico.</summary>
    Exame = 4,

    /// <summary>Cirurgia.</summary>
    Cirurgia = 5,

    /// <summary>Banho.</summary>
    Banho = 6,

    /// <summary>Tosa.</summary>
    Tosa = 7,

    /// <summary>Retorno de consulta anterior.</summary>
    Retorno = 8
}
