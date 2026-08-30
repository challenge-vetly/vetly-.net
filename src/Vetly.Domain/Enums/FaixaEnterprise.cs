namespace Vetly.Domain.Enums;

/// <summary>
/// Faixa de assinatura do plano Enterprise, definida pelo número de veterinários
/// vinculados à unidade (RN-072). A troca de faixa é automática ao cruzar o limite.
/// O valor em reais de cada faixa é decisão comercial e não é persistido aqui.
/// </summary>
public enum FaixaEnterprise
{
    /// <summary>1 a 5 veterinários — valor-base, cobre os 5 primeiros.</summary>
    De1a5 = 1,

    /// <summary>6 a 10 veterinários.</summary>
    De6a10 = 2,

    /// <summary>11 a 20 veterinários.</summary>
    De11a20 = 3,

    /// <summary>Acima de 20 veterinários — valor-base da faixa mais o adicional por vet.</summary>
    Acima20 = 4
}
