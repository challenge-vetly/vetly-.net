using Vetly.Application.DTOs.Documento;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de documentos clínicos.</summary>
public interface IDocumentoService
{
    Task<IEnumerable<DocumentoDto>> ObterPorConsultaAsync(Guid consultaId);
    Task<DocumentoDto> ObterPorIdAsync(Guid id);

    /// <summary>
    /// Cria um documento selecionando a Factory correta pelo tipo, com o conteudo
    /// formatado a partir do estado final aprovado pelo veterinario (RN-082/RN-083),
    /// e anexa o PDF renderizado (RN-090).
    /// </summary>
    Task<DocumentoDto> GerarAsync(Guid consultaId, TipoDocumento tipo, TipoAtestado? subtipo = null);

    /// <summary>
    /// Assina o documento pelo adaptador de assinatura (RN-087). Só o veterinário que
    /// conduziu o atendimento assina, e o carimbo entra no corpo do documento.
    /// </summary>
    Task<DocumentoDto> AssinarAsync(Guid id, string? nomeCompleto);

    /// <summary>Cria uma versão corrigida do documento (RN-088/RN-089).</summary>
    Task<DocumentoDto> CorrigirAsync(Guid id, string novosDados, string? justificativa, string crmvSolicitante);

    /// <summary>
    /// Publica o documento no board do pet, onde o Responsável o alcança
    /// (RN-011/RN-090). Idempotente: republicar preserva a data original.
    /// </summary>
    Task<DocumentoDto> PublicarAsync(Guid id);

    /// <summary>Documentos já publicados de um animal — o board do pet (RN-011/RN-090).</summary>
    Task<IEnumerable<DocumentoDto>> ObterDoBoardDoPetAsync(Guid animalId);

    /// <summary>Registra que o Responsável abriu o documento no app.</summary>
    Task<DocumentoDto> MarcarComoLidoAsync(Guid id);
}
