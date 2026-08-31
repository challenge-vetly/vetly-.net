using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>
/// Carteira do Responsável: o extrato financeiro do lado de quem paga
/// (RN-041/RN-071).
///
/// Reúne o que foi cobrado, o que foi devolvido e o que a fidelidade abateu. São as
/// três coisas que mexem no bolso do Responsável, e vê-las separadas é o que permite
/// conferir — um total único esconderia justamente a diferença entre "paguei" e
/// "me devolveram".
/// </summary>
public class CarteiraDoTutorDto
{
    public Guid TutorId { get; set; }

    /// <summary>Soma cobrada em transações confirmadas.</summary>
    public decimal TotalPago { get; set; }

    /// <summary>Soma devolvida em cancelamentos e reembolsos (RN-041).</summary>
    public decimal TotalEstornado { get; set; }

    /// <summary>Soma abatida por cupons de fidelidade (RN-051).</summary>
    public decimal TotalDeDescontos { get; set; }

    /// <summary>
    /// Sempre <c>Simulada</c> no MVP: os valores são apurados e registrados, nunca
    /// liquidados (RN-071). Prometer movimentação que não acontece seria pior que não
    /// mostrar.
    /// </summary>
    public string Liquidacao { get; set; } = "Simulada";

    public List<LancamentoDaCarteiraDto> Lancamentos { get; set; } = [];
}

/// <summary>Um lançamento na carteira do Responsável (RN-071).</summary>
public class LancamentoDaCarteiraDto
{
    public Guid PagamentoId { get; set; }
    public Guid? ConsultaId { get; set; }
    public Guid? InternacaoId { get; set; }

    public TipoPagamento Tipo { get; set; }
    public MeioPagamento MeioPagamento { get; set; }
    public StatusPagamento Status { get; set; }

    /// <summary>Valor bruto do serviço.</summary>
    public decimal Valor { get; set; }

    /// <summary>Desconto de fidelidade aplicado, quando houve (RN-051).</summary>
    public decimal? Desconto { get; set; }

    /// <summary>O que foi de fato cobrado: bruto menos desconto.</summary>
    public decimal ValorCobrado { get; set; }

    /// <summary>Valor devolvido, quando houve estorno (RN-041).</summary>
    public decimal? ValorEstornado { get; set; }

    public DateTime Momento { get; set; }
}
