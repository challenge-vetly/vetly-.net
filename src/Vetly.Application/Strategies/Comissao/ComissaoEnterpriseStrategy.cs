using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Comissao;

/// <summary>Comissão do plano Enterprise: 10% por transação, igual em todas as faixas (RN-089, RN-092).</summary>
public class ComissaoEnterpriseStrategy : IComissaoStrategy
{
    public bool Aplicavel(PlanoAssinatura plano) => plano == PlanoAssinatura.Enterprise;

    public decimal PercentualComissao => 10m;
}
