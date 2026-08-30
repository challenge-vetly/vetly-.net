using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Candidatos ao matching já filtrados pelo bounding box (§6.3).
/// </summary>
/// <param name="Autonomos">Veterinários autônomos elegíveis.</param>
/// <param name="Empresas">Clínicas elegíveis.</param>
/// <param name="VinculadosPorEmpresa">Veterinários publicados de cada clínica, por id da empresa.</param>
/// <param name="ServicosPorPrestador">Serviços ativos de cada prestador, por id.</param>
public readonly record struct CandidatosDoMatching(
    IReadOnlyList<Veterinario> Autonomos,
    IReadOnlyList<Empresa> Empresas,
    IReadOnlyDictionary<Guid, List<Veterinario>> VinculadosPorEmpresa,
    IReadOnlyDictionary<Guid, List<Servico>> ServicosPorPrestador);

/// <summary>
/// Contrato de leitura do matching (RN-001 a RN-033).
///
/// O filtro pesado — elegibilidade e bounding box — roda no banco; o score fica em
/// memória, sobre um conjunto já reduzido, como manda a §6.3.
/// </summary>
public interface IBuscaRepository
{
    /// <summary>
    /// Prestadores elegíveis dentro do retângulo informado: perfil publicado, CRMV
    /// válido, ativo no matching e com coordenada (RN-026/RN-107).
    /// </summary>
    Task<CandidatosDoMatching> ObterCandidatosAsync(
        decimal latMin, decimal latMax, decimal lngMin, decimal lngMax);

    /// <summary>Coordenada de um CEP, para o fallback de localização negada (RN-027).</summary>
    Task<(decimal Latitude, decimal Longitude)?> ObterCoordenadaDoCepAsync(string cep);
}
