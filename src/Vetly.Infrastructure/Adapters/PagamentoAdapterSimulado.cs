using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Pagamento simulado (camada C2, §5.1). Nenhuma chamada externa: o dinheiro do MVP
/// é registrado, nunca liquidado (RN-071).
///
/// A referência externa é derivada do id do pagamento (<c>sim_{pagamentoId}</c>), o
/// que torna a operação naturalmente idempotente: a mesma cobrança sempre devolve a
/// mesma referência.
///
/// Para exercitar a trilha de falha sem depender de sorte, o valor terminado em
/// <c>,99</c> é recusado — convenção do próprio documento de engenharia.
/// </summary>
public class PagamentoAdapterSimulado : IPagamentoAdapter
{
    private const string PrefixoDaReferencia = "sim_";

    /// <summary>Atraso com que o provedor simulado entrega o evento de status.</summary>
    public static readonly TimeSpan AtrasoDoEvento = TimeSpan.FromSeconds(2);

    private readonly ILogger<PagamentoAdapterSimulado> _logger;

    public PagamentoAdapterSimulado(ILogger<PagamentoAdapterSimulado> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task<CobrancaCriadaDto> CriarCobrancaAsync(CriarCobrancaRequest req)
    {
        var referencia = ReferenciaDe(req.PagamentoId);

        _logger.LogInformation(
            "Cobranca simulada criada | referencia={Referencia} valor={Valor} meio={Meio} chave={Chave}",
            referencia, req.Valor, req.Meio, req.ChaveIdempotencia);

        // O provedor simulado "manda" o evento alguns segundos depois, como um real faria.
        // O pagamento nao pode confirmar dentro da requisicao que criou a cobranca: o fluxo
        // perderia a forma assincrona que precisa ter quando um gateway real entrar.
        var desfecho = RecusaProgramada(req.Valor) ? StatusPagamento.Recusado : StatusPagamento.Confirmado;
        var evento = $"{{\"referenciaExterna\":\"{referencia}\",\"status\":\"{desfecho}\"}}";

        return Task.FromResult(new CobrancaCriadaDto(
            referencia, MontarInstrucoes(req), StatusPagamento.Pendente, evento, AtrasoDoEvento));
    }

    /// <inheritdoc/>
    public Task<StatusPagamento> ConsultarStatusAsync(string referenciaExterna)
    {
        // O simulado nao guarda estado proprio: a fonte de verdade e a base da Vetly,
        // alimentada pelo webhook. Consultar so confirma que a referencia e conhecida.
        var conhecida = referenciaExterna?.StartsWith(PrefixoDaReferencia, StringComparison.Ordinal) == true;

        return Task.FromResult(conhecida ? StatusPagamento.Pendente : StatusPagamento.Recusado);
    }

    /// <inheritdoc/>
    public Task<EstornoDto> EstornarAsync(EstornarRequest req)
    {
        if (req.Valor < 0)
            return Task.FromResult(new EstornoDto(false, 0, "Valor de estorno invalido."));

        _logger.LogInformation(
            "Estorno simulado | referencia={Referencia} valor={Valor} motivo={Motivo}",
            req.ReferenciaExterna, req.Valor, req.Motivo);

        // Responde na hora: no MVP nao ha liquidacao para esperar
        return Task.FromResult(new EstornoDto(true, req.Valor, "Estorno simulado registrado."));
    }

    /// <inheritdoc/>
    public Task<WebhookStatusDto> ReceberWebhookDeStatusAsync(string payloadBruto, string? assinaturaHeader)
    {
        try
        {
            using var documento = JsonDocument.Parse(payloadBruto);
            var raiz = documento.RootElement;

            var referencia = raiz.TryGetProperty("referenciaExterna", out var r) ? r.GetString() : null;
            var status = raiz.TryGetProperty("status", out var st) ? st.GetString() : null;

            if (string.IsNullOrWhiteSpace(referencia) || string.IsNullOrWhiteSpace(status))
                return Task.FromResult(new WebhookStatusDto(string.Empty, StatusPagamento.Recusado, false));

            if (!Enum.TryParse<StatusPagamento>(status, ignoreCase: true, out var statusParseado))
                return Task.FromResult(new WebhookStatusDto(referencia, StatusPagamento.Recusado, false));

            // O provedor real assina o payload; o simulado exige apenas que o token de
            // servico tenha chegado — a validacao dele fica na borda HTTP.
            var assinado = !string.IsNullOrWhiteSpace(assinaturaHeader);

            return Task.FromResult(new WebhookStatusDto(referencia, statusParseado, assinado));
        }
        catch (JsonException)
        {
            _logger.LogWarning("Webhook de pagamento com payload ilegivel foi descartado.");
            return Task.FromResult(new WebhookStatusDto(string.Empty, StatusPagamento.Recusado, false));
        }
    }

    /// <summary>Referência derivada do pagamento — a mesma cobrança sempre dá a mesma.</summary>
    public static string ReferenciaDe(Guid pagamentoId) => $"{PrefixoDaReferencia}{pagamentoId}";

    /// <summary>
    /// Convencao do documento: valor terminado em ,99 exercita a trilha de recusa sem
    /// depender de sorte.
    /// </summary>
    private static bool RecusaProgramada(decimal valor) => decimal.Round(valor % 1m, 2) == 0.99m;

    private static string MontarInstrucoes(CriarCobrancaRequest req)
    {
        var recusaProgramada = RecusaProgramada(req.Valor);

        var codigo = $"vetly-sim-{req.PagamentoId.ToString()[..8]}";

        return req.Meio switch
        {
            MeioPagamento.Pix => $"PixSimulado|{codigo}" + (recusaProgramada ? "|recusa-programada" : string.Empty),
            _ => $"CartaoSimulado|{codigo}" + (recusaProgramada ? "|recusa-programada" : string.Empty)
        };
    }
}
