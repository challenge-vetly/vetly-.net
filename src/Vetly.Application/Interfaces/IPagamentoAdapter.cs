using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Pedido de cobrança enviado ao adaptador de pagamento.</summary>
/// <param name="ChaveIdempotencia">Chave da transação: reenviar a mesma não duplica cobrança.</param>
/// <param name="PagamentoId">Pagamento que originou a cobrança.</param>
/// <param name="Valor">Valor bruto a cobrar.</param>
/// <param name="Meio">Meio de pagamento escolhido.</param>
public readonly record struct CriarCobrancaRequest(
    string ChaveIdempotencia, Guid PagamentoId, decimal Valor, MeioPagamento Meio);

/// <summary>Cobrança criada no provedor.</summary>
/// <param name="ReferenciaExterna">Identificador da cobrança no provedor.</param>
/// <param name="Instrucoes">Como pagar — código Pix, link, o que o provedor devolver.</param>
/// <param name="Status">Situação inicial. Nunca é confirmada aqui: quem confirma é o webhook.</param>
/// <param name="EventoSimulado">
/// Payload que o provedor enviaria ao webhook, e o atraso com que enviaria. Só o
/// adaptador simulado preenche isto — é assim que ele entrega o evento sem ninguém
/// precisar chamar a rota interna na mão. O adaptador real devolve nulo: quem manda
/// o evento é o provedor.
/// </param>
/// <param name="AtrasoDoEvento">Atraso até o envio do evento simulado.</param>
public readonly record struct CobrancaCriadaDto(
    string ReferenciaExterna, string Instrucoes, StatusPagamento Status,
    string? EventoSimulado = null, TimeSpan? AtrasoDoEvento = null);

/// <summary>Pedido de estorno.</summary>
/// <param name="ChaveIdempotencia">Chave do estorno: reenviar a mesma não estorna duas vezes.</param>
/// <param name="ReferenciaExterna">Cobrança a estornar.</param>
/// <param name="Valor">Valor a devolver — pode ser parcial (RN-014/RN-041).</param>
/// <param name="Motivo">Motivo registrado no provedor.</param>
public readonly record struct EstornarRequest(
    string ChaveIdempotencia, string ReferenciaExterna, decimal Valor, string Motivo);

/// <summary>Resultado do estorno.</summary>
/// <param name="Aceito">Se o provedor aceitou o estorno.</param>
/// <param name="ValorEstornado">Valor efetivamente devolvido.</param>
/// <param name="Mensagem">Detalhe legível, para log.</param>
public readonly record struct EstornoDto(bool Aceito, decimal ValorEstornado, string Mensagem);

/// <summary>Evento de mudança de status recebido do provedor.</summary>
/// <param name="ReferenciaExterna">Cobrança a que o evento se refere.</param>
/// <param name="Status">Novo status.</param>
/// <param name="Assinado">Se a assinatura do evento confere.</param>
public readonly record struct WebhookStatusDto(string ReferenciaExterna, StatusPagamento Status, bool Assinado);

/// <summary>
/// Porta de saída do pagamento (RN-006/RN-070/RN-071, §5.1 e vetly-tech §7.5).
///
/// Nenhum gateway está contratado nesta fase e o núcleo do produto nunca fala com um
/// diretamente. As quatro operações são simuladas no MVP, mas a fronteira já respeita
/// o que a troca por um fornecedor real vai exigir:
///
/// <list type="bullet">
///   <item><description><b>Idempotência</b> por chave: reenviar a mesma cobrança não
///   duplica nada;</description></item>
///   <item><description><b>Estado autoritativo no webhook</b>, nunca na resposta
///   síncrona. A confirmação da consulta reage ao evento, não ao retorno da chamada
///   (RN-006);</description></item>
///   <item><description><b>Split e desconto são calculados pela Vetly</b>
///   (RN-051/RN-070) — o adaptador só vê o valor bruto.</description></item>
/// </list>
/// </summary>
public interface IPagamentoAdapter
{
    /// <summary>Cria a cobrança. Nunca devolve pagamento confirmado.</summary>
    Task<CobrancaCriadaDto> CriarCobrancaAsync(CriarCobrancaRequest req);

    /// <summary>Consulta o status atual de uma cobrança.</summary>
    Task<StatusPagamento> ConsultarStatusAsync(string referenciaExterna);

    /// <summary>Estorna, total ou parcialmente, uma cobrança.</summary>
    Task<EstornoDto> EstornarAsync(EstornarRequest req);

    /// <summary>Interpreta e valida um evento de status recebido do provedor.</summary>
    Task<WebhookStatusDto> ReceberWebhookDeStatusAsync(string payloadBruto, string? assinaturaHeader);
}
