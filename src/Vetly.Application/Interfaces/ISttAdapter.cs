namespace Vetly.Application.Interfaces;

/// <summary>Pedido de transcrição de um segmento (§5.3).</summary>
/// <param name="SegmentoId">Segmento a transcrever.</param>
/// <param name="ConsultaId">Consulta de origem, para correlação nos logs.</param>
/// <param name="Sequencia">Ordem do segmento na consulta.</param>
/// <param name="AudioUrl">URL temporária de leitura do áudio, no storage.</param>
/// <param name="Formato">Tipo MIME do áudio.</param>
/// <param name="Idioma">Idioma esperado da fala.</param>
/// <param name="CallbackUrl">Endereço para onde o motor devolve o texto.</param>
/// <param name="CallbackToken">Token que o motor apresenta no callback.</param>
public readonly record struct SolicitarTranscricaoRequest(
    Guid SegmentoId,
    Guid ConsultaId,
    int Sequencia,
    string AudioUrl,
    string Formato,
    string Idioma,
    string CallbackUrl,
    string CallbackToken);

/// <summary>
/// Porta de saída da transcrição de fala (RN-009/RN-079, §5.3).
///
/// O motor recebe o áudio e devolve o texto <b>por callback</b> — o veterinário não
/// fica esperando, e a consulta segue enquanto a transcrição acontece.
///
/// Em produção, o fluxo Node-RED; trocar o motor é mexer dentro do fluxo, ou
/// substituir esta implementação. <b>O contrato do callback é da Vetly, não do
/// motor</b>: é o que permite trocar de fornecedor sem refazer o fluxo.
/// </summary>
public interface ISttAdapter
{
    /// <summary>
    /// Despacha o segmento para transcrição. Devolve <c>false</c> quando o motor não
    /// aceitou o trabalho — o chamador retenta com espera crescente.
    /// </summary>
    Task<bool> SolicitarTranscricaoAsync(SolicitarTranscricaoRequest req);
}
