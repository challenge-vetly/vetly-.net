using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Tabela de apoio da geocodificação simulada: CEP para coordenada (RN-026, §5.6).
///
/// Existe porque a RN-026 exige latitude/longitude <b>derivada do endereço
/// persistido</b>, e não dado mockado no front. Trocar pelo fornecedor real é trocar
/// a implementação do adaptador; esta tabela some junto.
/// </summary>
public class CepCoordenada
{
    /// <summary>CEP sem máscara, apenas dígitos (chave primária).</summary>
    [Key]
    [MaxLength(8)]
    public string Cep { get; private set; }

    /// <summary>Latitude do CEP.</summary>
    public decimal Latitude { get; private set; }

    /// <summary>Longitude do CEP.</summary>
    public decimal Longitude { get; private set; }

    /// <summary>Cidade, usada no fallback quando o CEP exato não está na base.</summary>
    [MaxLength(150)]
    public string Cidade { get; private set; }

    /// <summary>UF, usada junto da cidade no fallback.</summary>
    [MaxLength(2)]
    public string Uf { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private CepCoordenada()
    {
        Cep = null!;
        Cidade = null!;
        Uf = null!;
    }

    /// <summary>Cria uma entrada da tabela de apoio.</summary>
    public CepCoordenada(string cep, decimal latitude, decimal longitude, string cidade, string uf)
    {
        Cep = SomenteDigitos(cep);
        Latitude = latitude;
        Longitude = longitude;
        Cidade = cidade;
        Uf = uf.ToUpperInvariant();
    }

    /// <summary>Normaliza o CEP para apenas dígitos — o cliente manda com e sem máscara.</summary>
    public static string SomenteDigitos(string cep) =>
        new([.. (cep ?? string.Empty).Where(char.IsDigit)]);
}
