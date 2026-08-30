using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Comum;

/// <summary>
/// Coordenada derivada de um endereço pela geocodificação (RN-026, §5.6).
/// </summary>
public class CoordenadaDto
{
    /// <summary>Latitude. Nula quando não foi possível resolver o endereço.</summary>
    public decimal? Latitude { get; set; }

    /// <summary>Longitude. Nula quando não foi possível resolver o endereço.</summary>
    public decimal? Longitude { get; set; }

    /// <summary>Quão precisa é a coordenada.</summary>
    public PrecisaoCoordenada Precisao { get; set; }

    /// <summary>
    /// Verdadeiro quando a precisão é baixa demais para o matching e a coordenada
    /// deve ser revisada antes de valer.
    /// </summary>
    public bool Revisar { get; set; }

    /// <summary>Há coordenada utilizável.</summary>
    public bool Resolvida => Latitude is not null && Longitude is not null;
}
