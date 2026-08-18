using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Comissao;

/// <summary>Comissão do plano Profissional: 12% por transação (RN-089).</summary>
public class ComissaoProfissionalStrategy : IComissaoStrategy
{
    public bool Aplicavel(PlanoAssinatura plano) => plano == PlanoAssinatura.Profissional;

    public decimal PercentualComissao => 12m;
}
