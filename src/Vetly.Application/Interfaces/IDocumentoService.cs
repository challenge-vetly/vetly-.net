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

    /// <summary>
    /// Assina o documento por nome digitado (RN-031 — MVP): valida que o nome não é vazio
    /// e que coincide com o veterinário autenticado. Nunca habilita dispensação externa de
    /// controlados (RN-091).
    /// </summary>
    Task AssinarAsync(Guid id, string nomeDigitado);

    /// <summary>Cria uma versão corrigida do documento (RN-032/033/034).</summary>
    Task<DocumentoDto> CorrigirAsync(Guid id, string novosDados, string? justificativa, string crmvSolicitante);
}
