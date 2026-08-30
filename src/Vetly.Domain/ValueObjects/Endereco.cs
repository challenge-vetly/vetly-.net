namespace Vetly.Domain.ValueObjects;

/// <summary>
/// Endereço de um prestador (veterinário autônomo ou empresa), embutido no próprio
/// registro — modelo 1:1, sem tabela separada (RN-026).
/// A latitude/longitude é <b>derivada do endereço persistido</b> e é a fonte usada na
/// busca por proximidade; nunca um dado mockado no front (RN-026).
/// </summary>
public sealed class Endereco
{
    /// <summary>CEP do endereço, âncora da geocodificação (RN-026).</summary>
    public string Cep { get; private set; }

    /// <summary>Logradouro (rua, avenida).</summary>
    public string Logradouro { get; private set; }

    /// <summary>Número do imóvel.</summary>
    public string Numero { get; private set; }

    /// <summary>Complemento opcional (sala, bloco).</summary>
    public string? Complemento { get; private set; }

    /// <summary>Bairro — também usado no fallback de ordenação quando o Responsável nega a localização (RN-027).</summary>
    public string Bairro { get; private set; }

    /// <summary>Cidade.</summary>
    public string Cidade { get; private set; }

    /// <summary>UF do endereço. Não confundir com a UF de atuação do veterinário.</summary>
    public string Uf { get; private set; }

    /// <summary>Latitude derivada do endereço. Nula enquanto a geocodificação não rodou.</summary>
    public decimal? Latitude { get; private set; }

    /// <summary>Longitude derivada do endereço. Nula enquanto a geocodificação não rodou.</summary>
    public decimal? Longitude { get; private set; }

    /// <summary>
    /// Marca coordenada de baixa precisão (ex: CEP desconhecido resolvido para o centro
    /// da cidade), que deve ser revisada antes de valer para o matching.
    /// </summary>
    public bool CoordenadaRevisar { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Endereco()
    {
        Cep = null!;
        Logradouro = null!;
        Numero = null!;
        Bairro = null!;
        Cidade = null!;
        Uf = null!;
    }

    /// <summary>Cria um endereço. A coordenada entra depois, pela geocodificação.</summary>
    public Endereco(string cep, string logradouro, string numero, string bairro, string cidade, string uf, string? complemento = null)
    {
        if (string.IsNullOrWhiteSpace(cep)) throw new ArgumentException("O CEP é obrigatório.", nameof(cep));
        if (string.IsNullOrWhiteSpace(logradouro)) throw new ArgumentException("O logradouro é obrigatório.", nameof(logradouro));
        if (string.IsNullOrWhiteSpace(cidade)) throw new ArgumentException("A cidade é obrigatória.", nameof(cidade));
        if (string.IsNullOrWhiteSpace(uf) || uf.Length != 2) throw new ArgumentException("A UF deve ter 2 caracteres.", nameof(uf));

        Cep = cep.Trim();
        Logradouro = logradouro.Trim();
        Numero = numero?.Trim() ?? string.Empty;
        Complemento = string.IsNullOrWhiteSpace(complemento) ? null : complemento.Trim();
        Bairro = bairro?.Trim() ?? string.Empty;
        Cidade = cidade.Trim();
        Uf = uf.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Registra a coordenada derivada do endereço pela geocodificação (RN-026).
    /// <paramref name="revisar"/> sinaliza precisão insuficiente para o matching.
    /// </summary>
    public void DefinirCoordenada(decimal latitude, decimal longitude, bool revisar = false)
    {
        if (latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Latitude deve estar entre -90 e 90.");
        if (longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), "Longitude deve estar entre -180 e 180.");

        Latitude = latitude;
        Longitude = longitude;
        CoordenadaRevisar = revisar;
    }

    /// <summary>Verdadeiro quando há coordenada utilizável pelo matching (RN-026/RN-028).</summary>
    public bool TemCoordenada() => Latitude is not null && Longitude is not null;
}
