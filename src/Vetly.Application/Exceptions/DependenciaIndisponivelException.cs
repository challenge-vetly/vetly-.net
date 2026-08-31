namespace Vetly.Application.Exceptions;

/// <summary>
/// Uma dependência externa não respondeu (§2.4). Mapeia para HTTP 503.
///
/// Existe separada de <see cref="BusinessRuleException"/> porque a causa e a saída
/// são outras: não há regra violada nem nada que o cliente possa corrigir no payload
/// — o Ollama caiu, o Node-RED não respondeu, o conselho regional está fora. A
/// resposta certa é "tente de novo", e 422 diria ao app que a culpa é dele.
///
/// A distinção também importa para o RN-107: CRMV <c>Indisponivel</c> nunca é
/// aprovação por omissão, e um 503 deixa isso explícito para quem chamou.
/// </summary>
public class DependenciaIndisponivelException : Exception
{
    /// <summary>Qual dependência falhou: <c>Ollama</c>, <c>NodeRed</c>, <c>Crmv</c>…</summary>
    public string Dependencia { get; }

    /// <summary>Código da RN afetada, quando há uma.</summary>
    public string? Codigo { get; }

    public DependenciaIndisponivelException(string dependencia, string mensagem, string? codigo = null)
        : base(mensagem)
    {
        Dependencia = dependencia;
        Codigo = codigo;
    }
}
