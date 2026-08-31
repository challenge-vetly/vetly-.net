namespace Vetly.Application.Interfaces;

/// <summary>Um push a entregar em um dispositivo (RN-092).</summary>
/// <param name="PushToken">Token do dispositivo, emitido pelo APNs ou pelo FCM.</param>
/// <param name="Titulo">Título mostrado na notificação.</param>
/// <param name="Corpo">Texto da notificação.</param>
/// <param name="Destino">Rota interna que o app abre ao ser tocada.</param>
/// <param name="NotificacaoId">Id da notificação, para correlação nos logs.</param>
public readonly record struct EnvioDePushRequest(
    string PushToken,
    string Titulo,
    string Corpo,
    string? Destino,
    Guid NotificacaoId);

/// <summary>Resultado da tentativa de entrega (RN-092).</summary>
/// <param name="Entregue">Se o provedor aceitou o push.</param>
/// <param name="Erro">Motivo da recusa, quando houve.</param>
/// <param name="TokenInvalido">
/// Verdadeiro quando o provedor informou que o token não vale mais. É o que permite
/// desativar o dispositivo em vez de tentar para sempre um endereço morto.
/// </param>
public readonly record struct ResultadoDoPushDto(bool Entregue, string? Erro, bool TokenInvalido);

/// <summary>
/// Porta de saída do envio de push (RN-092, §5).
///
/// Fica atrás de porta porque o provedor é trocável e a diferença entre APNs, FCM e
/// um serviço próprio não deve chegar ao serviço de notificações. O que o domínio
/// precisa saber é apenas se o push foi aceito, e se o token morreu.
/// </summary>
public interface IPushAdapter
{
    /// <summary>Entrega um push a um dispositivo.</summary>
    Task<ResultadoDoPushDto> EnviarAsync(EnvioDePushRequest req);
}
