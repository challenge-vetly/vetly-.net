namespace Vetly.Domain.Enums;

/// <summary>
/// Forma de assinatura de um documento clínico. No MVP, só <see cref="NomeDigitado"/> é
/// aceito — certificado ICP-Brasil vinculado ao CRMV é alvo de produção (RN-031).
/// </summary>
public enum TipoAssinatura
{
    NomeDigitado = 1
}
