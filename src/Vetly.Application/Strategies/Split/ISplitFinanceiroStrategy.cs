using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Split;

/// <summary>
/// Repartição de uma transação entre a plataforma e o prestador (RN-070/RN-072).
/// </summary>
/// <param name="Plano">Plano que definiu o take rate.</param>
/// <param name="TakeRate">Percentual retido pela Vetly, de 0 a 100.</param>
/// <param name="Comissao">Valor retido pela Vetly.</param>
/// <param name="Repasse">Valor repassado ao prestador.</param>
public readonly record struct ResultadoDoSplit(
    PlanoAssinatura Plano, decimal TakeRate, decimal Comissao, decimal Repasse);

/// <summary>
/// Contrato do Strategy Pattern para o split financeiro.
///
/// O critério é o <b>plano de assinatura</b> (RN-070): Básico 15%, Profissional 12%,
/// Enterprise 10% — a maior comissão pertence ao menor plano, porque a escada troca
/// assinatura por comissão.
///
/// O repasse é <b>um só</b>: vai ao veterinário autônomo ou à empresa. A remuneração
/// interna dos vinculados é relação da clínica e está fora do escopo da plataforma
/// (RN-072).
/// </summary>
public interface ISplitFinanceiroStrategy
{
    /// <summary>Plano ao qual esta strategy se aplica.</summary>
    bool Aplicavel(PlanoAssinatura plano);

    /// <summary>Calcula a repartição de um valor bruto.</summary>
    ResultadoDoSplit Calcular(decimal valorBruto);
}
