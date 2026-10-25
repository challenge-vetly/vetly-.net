using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Pagamento;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de pagamentos.</summary>
public interface IPagamentoService
{
    /// <summary>
    /// Lista pagamentos paginados (§2.3), no escopo do token (RN-106), com filtros de
    /// status, tipo e período e a ordenação da whitelist
    /// <see cref="CamposDeOrdenacao.Pagamentos"/>.
    /// </summary>
    Task<ResultadoPaginado<PagamentoDto>> ObterTodosAsync(
        Paginacao paginacao, FiltroPagamentoDto? filtro = null, Ordenacao? ordenacao = null);
    Task<PagamentoDto> ObterPorIdAsync(Guid id);
    /// <summary>
    /// Cria a cobrança com o split já apurado. O pagamento fica pendente: quem
    /// confirma é o webhook (RN-006, vetly-tech §7.5).
    /// </summary>
    Task<CobrancaCriadaRespostaDto> CriarCobrancaAsync(CriarPagamentoDto dto);

    /// <summary>Status da cobrança e da consulta, para o polling do app (RN-006).</summary>
    Task<StatusDaCobrancaDto> ObterStatusAsync(Guid pagamentoId);

    /// <summary>
    /// Processa o evento de status do provedor — estado autoritativo do pagamento.
    /// Confirma ou expira a consulta e ocupa ou libera o horário (RN-006/RN-035).
    /// </summary>
    Task<ResultadoDoWebhookDto> ProcessarWebhookAsync(string payloadBruto, string? tokenDeServico);
    /// <summary>
    /// Carteira do Responsável: pagamentos, descontos e reembolsos (RN-041/RN-071).
    /// </summary>
    Task<CarteiraDoTutorDto> ObterCarteiraAsync(Guid tutorId);

    Task<PagamentoDto> ProcessarSplitAsync(Guid id);
}
