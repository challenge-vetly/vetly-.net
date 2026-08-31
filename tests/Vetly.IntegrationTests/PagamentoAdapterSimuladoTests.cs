using Microsoft.Extensions.Logging.Abstractions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Adapters;

namespace Vetly.IntegrationTests;

/// <summary>
/// Adaptador de pagamento simulado (camada C2, §5.1 e vetly-tech §7.5).
/// Fica neste projeto porque a implementacao vive na Infrastructure.
/// </summary>
public class PagamentoAdapterSimuladoTests
{
    private static PagamentoAdapterSimulado CriarAdapter() =>
        new(NullLogger<PagamentoAdapterSimulado>.Instance);

    private static CriarCobrancaRequest Cobranca(decimal valor, Guid? id = null) =>
        new("chave-de-teste", id ?? Guid.NewGuid(), valor, MeioPagamento.Pix);

    [Fact]
    public async Task CriarCobranca_NuncaDevolvePagamentoConfirmado()
    {
        var resultado = await CriarAdapter().CriarCobrancaAsync(Cobranca(200m));

        // O estado autoritativo vem do webhook, nao da resposta sincrona
        Assert.Equal(StatusPagamento.Pendente, resultado.Status);
    }

    [Fact]
    public async Task CriarCobranca_EIdempotentePelaReferencia()
    {
        var adapter = CriarAdapter();
        var pagamentoId = Guid.NewGuid();

        var primeira = await adapter.CriarCobrancaAsync(Cobranca(200m, pagamentoId));
        var segunda = await adapter.CriarCobrancaAsync(Cobranca(200m, pagamentoId));

        // Reenviar a mesma cobranca nao pode gerar duplicidade
        Assert.Equal(primeira.ReferenciaExterna, segunda.ReferenciaExterna);
        Assert.StartsWith("sim_", primeira.ReferenciaExterna);
    }

    [Fact]
    public async Task CriarCobranca_ValorTerminadoEmNoventaENove_MarcaARecusaProgramada()
    {
        var resultado = await CriarAdapter().CriarCobrancaAsync(Cobranca(199.99m));

        // Convencao do documento: exercita a trilha de falha sem depender de sorte
        Assert.Contains("recusa-programada", resultado.Instrucoes);
    }

    [Fact]
    public async Task CriarCobranca_ValorComum_NaoMarcaRecusa()
    {
        var resultado = await CriarAdapter().CriarCobrancaAsync(Cobranca(200m));

        Assert.DoesNotContain("recusa-programada", resultado.Instrucoes);
    }

    [Fact]
    public async Task Estornar_RespondeNaHora()
    {
        var resultado = await CriarAdapter().EstornarAsync(
            new EstornarRequest("chave", "sim_abc", 140m, "Cancelamento com 20h de antecedencia"));

        Assert.True(resultado.Aceito);
        Assert.Equal(140m, resultado.ValorEstornado);
    }

    [Fact]
    public async Task Estornar_ValorNegativo_NaoEAceito()
    {
        var resultado = await CriarAdapter().EstornarAsync(
            new EstornarRequest("chave", "sim_abc", -1m, "teste"));

        Assert.False(resultado.Aceito);
    }

    [Fact]
    public async Task Webhook_PayloadValidoComToken_EAceito()
    {
        var payload = """{"referenciaExterna":"sim_abc","status":"Confirmado"}""";

        var evento = await CriarAdapter().ReceberWebhookDeStatusAsync(payload, "token-de-servico");

        Assert.True(evento.Assinado);
        Assert.Equal("sim_abc", evento.ReferenciaExterna);
        Assert.Equal(StatusPagamento.Confirmado, evento.Status);
    }

    [Fact]
    public async Task Webhook_SemToken_NaoEConsideradoAssinado()
    {
        var payload = """{"referenciaExterna":"sim_abc","status":"Confirmado"}""";

        var evento = await CriarAdapter().ReceberWebhookDeStatusAsync(payload, null);

        Assert.False(evento.Assinado);
    }

    [Fact]
    public async Task Webhook_PayloadIlegivel_NaoDerrubaARota()
    {
        var evento = await CriarAdapter().ReceberWebhookDeStatusAsync("nao e json", "token");

        // Evento malformado e descartado, nao vira excecao nao tratada
        Assert.False(evento.Assinado);
        Assert.Equal(string.Empty, evento.ReferenciaExterna);
    }

    [Fact]
    public async Task Webhook_StatusDesconhecido_NaoEAceito()
    {
        var payload = """{"referenciaExterna":"sim_abc","status":"InventadoPeloProvedor"}""";

        var evento = await CriarAdapter().ReceberWebhookDeStatusAsync(payload, "token");

        Assert.False(evento.Assinado);
    }

    [Fact]
    public async Task ConsultarStatus_ReferenciaDesconhecida_NaoEValida()
    {
        var status = await CriarAdapter().ConsultarStatusAsync("referencia-de-outro-provedor");

        Assert.Equal(StatusPagamento.Recusado, status);
    }
}
