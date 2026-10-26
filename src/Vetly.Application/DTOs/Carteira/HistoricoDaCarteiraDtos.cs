using Vetly.Application.DTOs.Comum;

namespace Vetly.Application.DTOs.Carteira;

/// <summary>O que um lançamento do histórico da carteira representa (RN-115).</summary>
public enum TipoDeLancamento
{
    /// <summary>Cobrança de consulta, caução ou saldo de internação.</summary>
    Pagamento = 1,

    /// <summary>Troca de pontos por um item do marketplace (RN-053).</summary>
    Resgate = 2,

    /// <summary>Devolução total ou parcial de um pagamento (RN-041).</summary>
    Reembolso = 3
}

/// <summary>
/// Um lançamento do histórico unificado da carteira (RN-115): pagamento, resgate ou
/// reembolso, no mesmo formato.
///
/// Os três vêm de lugares diferentes — pagamento e reembolso de <c>TB_PAGAMENTO</c>,
/// resgate de <c>TB_CUPOM_RESGATE</c> — e o app os mostra numa linha do tempo só. O
/// formato comum é o que deixa a tela ser uma lista, e não três abas.
/// </summary>
public class LancamentoDoHistoricoDto
{
    /// <summary>Pagamento, resgate ou reembolso.</summary>
    public TipoDeLancamento Tipo { get; set; }

    /// <summary>Quando aconteceu (UTC).</summary>
    public DateTime Momento { get; set; }

    /// <summary>Texto da linha: o que foi pago, resgatado ou devolvido.</summary>
    public string Descricao { get; set; } = string.Empty;

    /// <summary>
    /// Valor em reais, sempre positivo — o <see cref="Tipo"/> diz o sentido. No
    /// pagamento, o cobrado; no reembolso, o devolvido; no resgate, o desconto que
    /// o cupom vale (RN-049).
    /// </summary>
    public decimal? ValorEmReais { get; set; }

    /// <summary>Pontos debitados, só no resgate.</summary>
    public int? Pontos { get; set; }

    /// <summary>
    /// Situação na origem: o status do pagamento (pagamento e reembolso) ou do cupom
    /// (resgate).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Pagamento de origem (pagamento e reembolso).</summary>
    public Guid? PagamentoId { get; set; }

    /// <summary>Consulta paga, quando houver.</summary>
    public Guid? ConsultaId { get; set; }

    /// <summary>Internação paga, quando houver.</summary>
    public Guid? InternacaoId { get; set; }

    /// <summary>Cupom emitido (resgate).</summary>
    public Guid? CupomId { get; set; }

    /// <summary>Item do catálogo resgatado, quando o resgate veio do catálogo.</summary>
    public Guid? ItemMarketplaceId { get; set; }
}

/// <summary>
/// Totais do histórico, calculados sobre o <b>filtro</b> — não sobre a página. Quem
/// filtra outubro quer saber quanto pagou em outubro, e não quanto cabe em 20 linhas.
/// </summary>
public class ResumoDaCarteiraDto
{
    /// <summary>
    /// Soma cobrada: pagamentos confirmados e também os estornados — o dinheiro saiu, e
    /// o que voltou está em <see cref="TotalEstornado"/>.
    /// </summary>
    public decimal TotalPago { get; set; }

    /// <summary>Soma devolvida (RN-041).</summary>
    public decimal TotalEstornado { get; set; }

    /// <summary>Soma abatida por cupons de fidelidade nos pagamentos (RN-051).</summary>
    public decimal TotalDeDescontos { get; set; }

    /// <summary>Pontos trocados por itens do marketplace.</summary>
    public int PontosResgatados { get; set; }

    /// <summary>Sempre <c>Simulada</c> no MVP: valores registrados, não liquidados (RN-071).</summary>
    public string Liquidacao { get; set; } = "Simulada";
}

/// <summary>
/// Página do histórico da carteira com o resumo do filtro. Mesmo envelope de toda
/// listagem paginada (<c>itens</c>, <c>total</c>, <c>pagina</c>…) mais <c>resumo</c>.
/// </summary>
public class HistoricoDaCarteiraDto : ResultadoPaginado<LancamentoDoHistoricoDto>, IPaginaComResumo
{
    /// <summary>Responsável dono da carteira.</summary>
    public Guid TutorId { get; init; }

    /// <summary>Totais do filtro inteiro.</summary>
    public ResumoDaCarteiraDto Resumo { get; init; } = new();

    /// <inheritdoc/>
    object IPaginaComResumo.Resumo => Resumo;

    /// <summary>Monta a página com o resumo.</summary>
    public HistoricoDaCarteiraDto(
        Guid tutorId, IReadOnlyList<LancamentoDoHistoricoDto> itens, int total, Paginacao paginacao, ResumoDaCarteiraDto resumo)
        : base(itens, total, paginacao)
    {
        TutorId = tutorId;
        Resumo = resumo;
    }
}

/// <summary>
/// Filtros do histórico (<c>GET /api/tutores/{id}/carteira/historico</c>). Ordenação
/// por <c>ordenarPor=momento|valor</c>.
/// </summary>
public class FiltroHistoricoDaCarteiraDto
{
    /// <summary>Só lançamentos deste tipo.</summary>
    public TipoDeLancamento? Tipo { get; set; }

    /// <summary>Início do período, inclusivo.</summary>
    public DateTime? De { get; set; }

    /// <summary>Fim do período, inclusivo.</summary>
    public DateTime? Ate { get; set; }

    /// <summary>
    /// Situação na origem, sem distinção de caixa (ex.: <c>Confirmado</c>,
    /// <c>Estornado</c>, <c>Emitido</c>, <c>Resgatado</c>).
    /// </summary>
    public string? Status { get; set; }
}
