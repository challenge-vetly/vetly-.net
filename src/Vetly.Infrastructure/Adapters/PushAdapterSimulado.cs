using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Push simulado, para desenvolvimento sem APNs nem FCM (RN-092, §5).
///
/// Registra o envio no log e devolve entregue. É deliberadamente otimista: em
/// desenvolvimento, um push que "falha" só cria ruído para quem está testando o
/// fluxo de negócio.
///
/// A única falha que ele reproduz é a que importa reproduzir — <b>token
/// evidentemente inválido</b>. É o caso que exercita o caminho de desativar o
/// dispositivo, e é o que mais acontece em produção: app desinstalado, token
/// rotacionado, permissão revogada.
/// </summary>
public class PushAdapterSimulado : IPushAdapter
{
    private readonly ILogger<PushAdapterSimulado> _logger;

    /// <summary>Tamanho abaixo do qual um token não é plausível.</summary>
    private const int TamanhoMinimoDoToken = 8;

    public PushAdapterSimulado(ILogger<PushAdapterSimulado> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task<ResultadoDoPushDto> EnviarAsync(EnvioDePushRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PushToken) || req.PushToken.Length < TamanhoMinimoDoToken)
        {
            _logger.LogWarning(
                "Push simulado recusado: token invalido | notificacao={NotificacaoId}", req.NotificacaoId);

            return Task.FromResult(new ResultadoDoPushDto(
                Entregue: false, Erro: "Token de push invalido.", TokenInvalido: true));
        }

        _logger.LogInformation(
            "Push simulado entregue | notificacao={NotificacaoId} titulo=\"{Titulo}\" destino={Destino}",
            req.NotificacaoId, req.Titulo, req.Destino ?? "-");

        return Task.FromResult(new ResultadoDoPushDto(Entregue: true, Erro: null, TokenInvalido: false));
    }
}
