using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Contrato de repositório específico para a entidade <see cref="Documento"/>.</summary>
public interface IDocumentoRepository : IRepositoryBase<Documento>
{
    /// <summary>Retorna todos os documentos vinculados a uma consulta.</summary>
    Task<IEnumerable<Documento>> ObterPorConsultaAsync(Guid consultaId);

    /// <summary>Retorna todos os documentos vinculados a uma internação.</summary>
    Task<IEnumerable<Documento>> ObterPorInternacaoAsync(Guid internacaoId);

    /// <summary>Retorna documentos de um tipo específico vinculados a uma consulta.</summary>
    Task<Documento?> ObterPorConsultaETipoAsync(Guid consultaId, TipoDocumento tipo);
}
