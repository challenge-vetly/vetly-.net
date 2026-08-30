using Vetly.Application.DTOs.Documento;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de documentos clínicos.</summary>
public interface IDocumentoService
{
    Task<IEnumerable<DocumentoDto>> ObterPorConsultaAsync(Guid consultaId);
    Task<DocumentoDto> ObterPorIdAsync(Guid id);

    /// <summary>Cria um documento selecionando a Factory correta pelo tipo, a partir do estado final (RN-082/RN-083).</summary>
    Task<DocumentoDto> GerarAsync(Guid consultaId, TipoDocumento tipo);

    /// <summary>Assina digitalmente o documento (RN-087).</summary>
    Task AssinarAsync(Guid id);

    /// <summary>Cria uma versão corrigida do documento (RN-088/RN-089).</summary>
    Task<DocumentoDto> CorrigirAsync(Guid id, string novosDados, string? justificativa, string crmvSolicitante);
}
