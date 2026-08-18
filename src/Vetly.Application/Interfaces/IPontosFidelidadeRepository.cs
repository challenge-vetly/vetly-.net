using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="PontosFidelidade"/>.</summary>
public interface IPontosFidelidadeRepository : IRepositoryBase<PontosFidelidade>
{
    /// <summary>Retorna todos os lançamentos de um responsável, mais recentes primeiro (extrato).</summary>
    Task<IEnumerable<PontosFidelidade>> ObterPorResponsavelAsync(Guid responsavelId);

    /// <summary>Retorna o lançamento gerado por uma consulta específica, se houver (usado no estorno — RN-075).</summary>
    Task<PontosFidelidade?> ObterPorConsultaAsync(Guid consultaId);
}
