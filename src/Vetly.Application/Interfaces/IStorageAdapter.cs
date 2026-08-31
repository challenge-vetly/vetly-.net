namespace Vetly.Application.Interfaces;

/// <summary>URL temporária para o app falar direto com o storage (§2.6).</summary>
/// <param name="Url">Endereço a usar.</param>
/// <param name="ExpiraEm">Instante em que a URL deixa de valer.</param>
public readonly record struct UrlAssinadaDto(string Url, DateTime ExpiraEm);

/// <summary>
/// Porta de saída do storage de objetos (§2.6, §11).
///
/// A API <b>nunca proxia os bytes</b>: ela registra a mídia, entrega uma URL
/// temporária e o app envia ou baixa direto. Áudio de consulta e imagem clínica não
/// passam pelo processo da API — CLOB no Oracle também não é lugar para binário de
/// áudio.
///
/// Em produção é um bucket S3-compatível; em desenvolvimento, disco local.
/// </summary>
public interface IStorageAdapter
{
    /// <summary>URL para o app enviar o arquivo.</summary>
    Task<UrlAssinadaDto> GerarUrlDeUploadAsync(string chave, string contentType, TimeSpan validade);

    /// <summary>
    /// URL para ler o arquivo. Conteúdo clínico nunca vira URL pública e permanente
    /// (RN-090) — é sempre temporária e emitida sob autorização.
    /// </summary>
    Task<UrlAssinadaDto> GerarUrlDeLeituraAsync(string chave, TimeSpan validade);

    /// <summary>Indica se o objeto já foi enviado.</summary>
    Task<bool> ExisteAsync(string chave);

    /// <summary>Tamanho do objeto em bytes, ou nulo se ele não existe.</summary>
    Task<long?> ObterTamanhoAsync(string chave);

    /// <summary>Remove o objeto — retenção vencida ou exclusão explícita.</summary>
    Task RemoverAsync(string chave);
}
