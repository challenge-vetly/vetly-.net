using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>
/// Filtros da listagem de pagamentos (<c>GET /api/pagamentos</c>).
///
/// Não há filtro por Responsável de propósito: o escopo vem do token (RN-106). Um
/// <c>?tutorId=</c> aqui seria a porta para um Responsável pedir a carteira de outro,
/// e para o Admin ele não acrescenta nada que a listagem consolidada já não mostre.
/// </summary>
public class FiltroPagamentoDto
{
    /// <summary>Filtra pelo estado da cobrança (pendente, confirmado, recusado…).</summary>
    public StatusPagamento? Status { get; set; }

    /// <summary>Filtra pelo que foi cobrado: consulta, caução ou saldo de internação (RN-101).</summary>
    public TipoPagamento? Tipo { get; set; }

    /// <summary>Início do período, inclusivo, sobre o momento da cobrança.</summary>
    public DateTime? De { get; set; }

    /// <summary>Fim do período, inclusivo, sobre o momento da cobrança.</summary>
    public DateTime? Ate { get; set; }
}
