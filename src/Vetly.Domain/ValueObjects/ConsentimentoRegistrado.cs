using Vetly.Domain.Enums;

namespace Vetly.Domain.ValueObjects;

/// <summary>
/// Leitura do estado de uma finalidade de consentimento, com as datas de concessão
/// e de revogação (RN-061/RN-062). Projeção somente-leitura: o estado vive na entidade
/// <c>Tutor</c>, em colunas próprias.
/// </summary>
/// <param name="Finalidade">Finalidade a que este registro se refere.</param>
/// <param name="Concedido">Se a finalidade está autorizada neste momento.</param>
/// <param name="ConcedidoEm">Quando foi concedida pela última vez.</param>
/// <param name="RevogadoEm">Quando foi revogada pela última vez.</param>
public readonly record struct ConsentimentoRegistrado(
    FinalidadeConsentimento Finalidade,
    bool Concedido,
    DateTime? ConcedidoEm,
    DateTime? RevogadoEm);
