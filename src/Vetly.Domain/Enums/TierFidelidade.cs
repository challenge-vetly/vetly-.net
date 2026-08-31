namespace Vetly.Domain.Enums;

/// <summary>
/// Faixa do programa de fidelidade, calculada sobre o acúmulo dos últimos 12 meses
/// (RN-048).
///
/// O multiplicador cresce no topo porque é o que sustenta recorrência: quem já usa
/// muito ganha mais por continuar. As faixas foram calibradas contra o ganho da
/// RN-047 para serem alcançáveis — um Responsável de um pet com o calendário em dia
/// chega a Prata no primeiro ano.
/// </summary>
public enum TierFidelidade
{
    /// <summary>0 a 999 pontos em 12 meses. Multiplicador 1,0×.</summary>
    Bronze = 1,

    /// <summary>1.000 a 2.999 pontos em 12 meses. Multiplicador 1,25×.</summary>
    Prata = 2,

    /// <summary>3.000 pontos ou mais em 12 meses. Multiplicador 1,5×.</summary>
    Ouro = 3
}

/// <summary>
/// Faixa de financiamento do desconto de fidelidade (RN-051).
///
/// Descontos pequenos não oneram o vet, o que preserva a adesão ao programa;
/// resgates grandes são co-financiados por quem captura a recorrência. A faixa é
/// determinada pelo <b>valor em reais</b> do desconto, não pelos pontos.
/// </summary>
public enum FaixaDeFinanciamento
{
    /// <summary>Até R$ 10,00 — 100% Vetly.</summary>
    Ate10 = 1,

    /// <summary>De R$ 10,01 a R$ 30,00 — 60% Vetly / 40% prestador.</summary>
    De10a30 = 2,

    /// <summary>Acima de R$ 30,00 — 30% Vetly / 70% prestador.</summary>
    Acima30 = 3
}

/// <summary>Situação de um cupom de resgate (RN-053/RN-054).</summary>
public enum StatusCupom
{
    /// <summary>Emitido e dentro da validade de 30 dias.</summary>
    Emitido = 1,

    /// <summary>Apresentado e validado no estabelecimento. Validação física é mock no MVP (RN-019).</summary>
    Resgatado = 2,

    /// <summary>Venceu sem uso. Os pontos <b>não</b> retornam ao saldo (RN-053).</summary>
    Expirado = 3
}

/// <summary>Categoria do item resgatado, na taxonomia que sustenta a taxa de listagem (RN-099).</summary>
public enum CategoriaItem
{
    Alimentacao = 1,
    Medicamentos = 2,
    Higiene = 3
}
