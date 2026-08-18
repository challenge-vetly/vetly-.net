using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Comissao;

/// <summary>Comissão do plano Básico: 15% por transação (RN-089).</summary>
public class ComissaoBasicoStrategy : IComissaoStrategy
{
    public bool Aplicavel(PlanoAssinatura plano) => plano == PlanoAssinatura.Basico;

    public decimal PercentualComissao => 15m;
}
