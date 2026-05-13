using Vetly.Application.DTOs.Documento;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de documentos clínicos.</summary>
public interface IDocumentoService
{
    Task<IEnumerable<DocumentoDto>> ObterPorConsultaAsync(Guid consultaId);
    Task<DocumentoDto> ObterPorIdAsync(Guid id);

    /// <summary>Cria um documento selecionando a Factory correta pelo tipo (RN-024).</summary>
    Task<DocumentoDto> GerarAsync(Guid consultaId, TipoDocumento tipo);

    /// <summary>Assina digitalmente o documento (RN-031).</summary>
    Task AssinarAsync(Guid id);

    /// <summary>Cria uma versão corrigida do documento (RN-032/033/034).</summary>
    Task<DocumentoDto> CorrigirAsync(Guid id, string novosDados, string? justificativa, string crmvSolicitante);
}
