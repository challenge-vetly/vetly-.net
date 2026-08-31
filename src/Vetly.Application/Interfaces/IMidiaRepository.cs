using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="Midia"/> (§2.6).</summary>
public interface IMidiaRepository
{
    Task<Midia?> ObterPorIdAsync(Guid id);

    /// <summary>Busca pela chave do storage — é assim que a rota de upload acha a mídia.</summary>
    Task<Midia?> ObterPorChaveAsync(string chaveStorage);

    /// <summary>Mídias de uma consulta.</summary>
    Task<IEnumerable<Midia>> ObterDaConsultaAsync(Guid consultaId);

    Task AdicionarAsync(Midia midia);
    void Atualizar(Midia midia);
    Task<int> SalvarAsync();
}
