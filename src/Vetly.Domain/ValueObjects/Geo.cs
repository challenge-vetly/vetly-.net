namespace Vetly.Domain.ValueObjects;

/// <summary>
/// Cálculos geográficos do matching (§6.3 do documento de engenharia).
///
/// Sem PostGIS e sem Oracle Spatial: a busca filtra por <b>bounding box</b> — que é
/// aritmética simples e usa índice — e só então calcula a distância real por
/// Haversine, sobre o conjunto já reduzido.
/// </summary>
public static class Geo
{
    /// <summary>Raio médio da Terra em quilômetros.</summary>
    public const double RaioDaTerraKm = 6371.0;

    /// <summary>Comprimento aproximado de um grau de latitude, em quilômetros.</summary>
    private const double KmPorGrauDeLatitude = 111.0;

    /// <summary>
    /// Retângulo que contém, com folga, todos os pontos dentro do raio informado.
    /// É o filtro que roda no banco: comparação de intervalo em duas colunas indexadas,
    /// sem trigonometria.
    /// </summary>
    /// <remarks>
    /// O retângulo é sempre maior que o círculo, então ele nunca exclui um resultado
    /// válido — apenas admite alguns a mais, que o Haversine descarta depois.
    /// </remarks>
    public static (decimal LatMin, decimal LatMax, decimal LngMin, decimal LngMax) CalcularBoundingBox(
        decimal latitude, decimal longitude, double raioKm)
    {
        var deltaLat = raioKm / KmPorGrauDeLatitude;

        // Um grau de longitude encurta conforme se afasta do equador; sem o cosseno,
        // a caixa ficaria estreita demais em latitudes altas e perderia resultados.
        var cosseno = Math.Cos(GrausParaRadianos((double)latitude));
        var deltaLng = raioKm / (KmPorGrauDeLatitude * Math.Max(Math.Abs(cosseno), 0.01));

        return (
            latitude - (decimal)deltaLat,
            latitude + (decimal)deltaLat,
            longitude - (decimal)deltaLng,
            longitude + (decimal)deltaLng);
    }

    /// <summary>
    /// Distância em quilômetros entre dois pontos, pela fórmula de Haversine.
    /// </summary>
    public static double DistanciaEmKm(decimal latOrigem, decimal lngOrigem, decimal latDestino, decimal lngDestino)
    {
        var lat1 = GrausParaRadianos((double)latOrigem);
        var lat2 = GrausParaRadianos((double)latDestino);
        var deltaLat = lat2 - lat1;
        var deltaLng = GrausParaRadianos((double)(lngDestino - lngOrigem));

        var a = (Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2)) +
                (Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(deltaLng / 2) * Math.Sin(deltaLng / 2));

        return RaioDaTerraKm * 2 * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
    }

    private static double GrausParaRadianos(double graus) => graus * Math.PI / 180.0;
}
