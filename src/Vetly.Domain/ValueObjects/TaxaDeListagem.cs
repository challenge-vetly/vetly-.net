using Vetly.Domain.Enums;

namespace Vetly.Domain.ValueObjects;

/// <summary>
/// Taxa que o fornecedor pagaria para listar um item no marketplace, por categoria
/// (RN-099/RN-111).
///
/// <para>
/// <b>Registrada, não cobrada.</b> No MVP não há portal do parceiro nem faturamento
/// de fornecedor: a taxa existe para que o modelo de receita do marketplace seja
/// visível e calculável desde já (o Admin consulta a tabela), sem nenhuma cobrança
/// acontecer. Quando houver faturamento, ele lê daqui — e a recalibração é num
/// lugar só, como em <see cref="RegrasDeFidelidade"/>.
/// </para>
/// <para>
/// Medicamentos custam mais por ter margem maior e exigir curadoria (procedência,
/// registro); higiene custa menos para atrair o tipo de item que gera recorrência.
/// </para>
/// </summary>
public static class TaxaDeListagem
{
    /// <summary>Taxa de listagem da categoria, em reais.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Categoria desconhecida.</exception>
    public static decimal Para(CategoriaItem categoria) => categoria switch
    {
        CategoriaItem.Alimentacao => 150.00m,
        CategoriaItem.Medicamentos => 250.00m,
        CategoriaItem.Higiene => 100.00m,
        _ => throw new ArgumentOutOfRangeException(nameof(categoria), "Categoria de item desconhecida.")
    };
}
