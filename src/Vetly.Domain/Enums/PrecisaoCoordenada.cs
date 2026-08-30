namespace Vetly.Domain.Enums;

/// <summary>
/// Precisão da coordenada derivada do endereço (RN-026).
/// Quanto pior a precisão, menos confiável é a distância calculada no matching.
/// </summary>
public enum PrecisaoCoordenada
{
    /// <summary>Não foi possível derivar coordenada — o endereço fica sem posição.</summary>
    Desconhecida = 0,

    /// <summary>Resolvida até o endereço exato. Melhor precisão.</summary>
    Endereco = 1,

    /// <summary>Resolvida pelo CEP. Precisão suficiente para o matching.</summary>
    Cep = 2,

    /// <summary>
    /// Resolvida pelo centro da cidade ou do bairro. Serve para não travar o cadastro,
    /// mas marca a coordenada para revisão antes de valer no matching.
    /// </summary>
    Bairro = 3
}
