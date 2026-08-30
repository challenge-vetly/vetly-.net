using Vetly.Application.DTOs.Comum;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Porta de saída da geocodificação (RN-026, §5.6).
///
/// Existe porque a RN-026 exige que a latitude/longitude seja <b>derivada do endereço
/// persistido</b> — nunca informada pelo cliente nem mockada no front. Trocar o
/// fornecedor real é trocar o registro no DI.
/// </summary>
public interface IGeocodificacaoAdapter
{
    /// <summary>
    /// Resolve a coordenada de um endereço. Nunca lança por endereço desconhecido:
    /// devolve <c>Precisao.Desconhecida</c> e cabe ao chamador decidir o que fazer.
    /// </summary>
    Task<CoordenadaDto> GeocodificarAsync(EnderecoDto endereco);
}
