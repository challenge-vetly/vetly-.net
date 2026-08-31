using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório específico para a entidade <see cref="Documento"/>.</summary>
public interface IDocumentoRepository : IRepositoryBase<Documento>
{
    /// <summary>Retorna todos os documentos vinculados a uma consulta.</summary>
    Task<IEnumerable<Documento>> ObterPorConsultaAsync(Guid consultaId);

    /// <summary>Retorna todos os documentos vinculados a uma internação.</summary>
    Task<IEnumerable<Documento>> ObterPorInternacaoAsync(Guid internacaoId);

    /// <summary>Retorna documentos de um tipo específico vinculados a uma consulta.</summary>
    Task<Documento?> ObterPorConsultaETipoAsync(Guid consultaId, TipoDocumento tipo);

    /// <summary>
    /// Documentos publicados de um animal, do mais recente ao mais antigo — o board
    /// do pet (RN-011/RN-090). Documento gerado mas ainda não publicado não aparece:
    /// o Responsável não deve ver rascunho de documento.
    /// </summary>
    Task<IEnumerable<Documento>> ObterPublicadosPorAnimalAsync(Guid animalId);
}
