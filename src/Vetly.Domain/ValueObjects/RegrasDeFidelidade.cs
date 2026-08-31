using Vetly.Domain.Enums;

namespace Vetly.Domain.ValueObjects;

/// <summary>
/// Os parâmetros fechados do programa de fidelidade (vetly-tech §1).
///
/// Ficam num só lugar porque são valores de produto, calibrados contra referência de
/// mercado (§7.1), e não decisões de implementação. Espalhá-los pelos serviços faria
/// uma recalibração futura virar caça a constantes.
/// </summary>
public static class RegrasDeFidelidade
{
    /// <summary>1 ponto por R$ 1 gasto, arredondado para baixo (RN-047).</summary>
    public const int PontosPorReal = 1;

    /// <summary>
    /// 50 pontos fixos por obrigação do pet cumprida no prazo (RN-047).
    ///
    /// É o que diferencia o programa: recompensa <b>comportamento de cuidado</b>, e
    /// não só gasto. Um Responsável que mantém a vacinação em dia pontua mesmo em ano
    /// de baixa despesa.
    /// </summary>
    public const int PontosPorObrigacaoCumprida = 50;

    /// <summary>
    /// 100 pontos valem R$ 3,00 de desconto (RN-049).
    ///
    /// Calibrado sobre o retorno efetivo de programas de saúde pet consolidados
    /// (~3% do gasto). É o teto que cabe dentro de um take rate de 10 a 15% —
    /// conversão mais generosa devolveria mais do que a comissão inteira.
    /// </summary>
    public const decimal ReaisPor100Pontos = 3.00m;

    /// <summary>Validade do crédito: 12 meses, consumidos em FIFO (RN-050).</summary>
    public static readonly TimeSpan ValidadeDosPontos = TimeSpan.FromDays(365);

    /// <summary>Validade do cupom emitido no resgate (RN-053).</summary>
    public static readonly TimeSpan ValidadeDoCupom = TimeSpan.FromDays(30);

    /// <summary>Janela móvel sobre a qual o tier é reavaliado (RN-048/RN-050).</summary>
    public static readonly TimeSpan JanelaDoTier = TimeSpan.FromDays(365);

    /// <summary>Converte pontos em reais de desconto (RN-049).</summary>
    public static decimal EmReais(int pontos) =>
        Math.Round(pontos * ReaisPor100Pontos / 100m, 2, MidpointRounding.ToZero);

    /// <summary>
    /// Quantos pontos são necessários para um desconto de determinado valor (RN-049).
    /// Arredonda para cima: não se dá desconto que os pontos não cobrem.
    /// </summary>
    public static int PontosPara(decimal reais) =>
        (int)Math.Ceiling(reais * 100m / ReaisPor100Pontos);

    /// <summary>
    /// Tier a partir do acúmulo dos últimos 12 meses (RN-048).
    ///
    /// O acúmulo conta os pontos <b>creditados</b> na janela, não o saldo: quem
    /// resgatou não perde o tier por ter usado o programa, que é justamente o
    /// comportamento que o programa quer.
    /// </summary>
    public static TierFidelidade TierPara(int acumuloEm12Meses) => acumuloEm12Meses switch
    {
        >= 3000 => TierFidelidade.Ouro,
        >= 1000 => TierFidelidade.Prata,
        _ => TierFidelidade.Bronze
    };

    /// <summary>Multiplicador de ganho do tier, aplicado sobre os pontos brutos (RN-048).</summary>
    public static decimal MultiplicadorDe(TierFidelidade tier) => tier switch
    {
        TierFidelidade.Ouro => 1.5m,
        TierFidelidade.Prata => 1.25m,
        _ => 1.0m
    };

    /// <summary>Faixa de financiamento pelo valor do desconto (RN-051).</summary>
    public static FaixaDeFinanciamento FaixaPara(decimal desconto) => desconto switch
    {
        <= 10.00m => FaixaDeFinanciamento.Ate10,
        <= 30.00m => FaixaDeFinanciamento.De10a30,
        _ => FaixaDeFinanciamento.Acima30
    };

    /// <summary>
    /// Divide o custo do desconto entre a Vetly e o prestador (RN-051).
    ///
    /// A parte do prestador sai por subtração para que as duas sempre fechem o
    /// desconto — arredondar as duas separadamente deixaria centavos sem dono.
    /// </summary>
    public static (decimal Vetly, decimal Prestador, FaixaDeFinanciamento Faixa) Dividir(decimal desconto)
    {
        var faixa = FaixaPara(desconto);

        var percentualVetly = faixa switch
        {
            FaixaDeFinanciamento.Ate10 => 100m,
            FaixaDeFinanciamento.De10a30 => 60m,
            _ => 30m
        };

        var parteVetly = Math.Round(desconto * percentualVetly / 100m, 2, MidpointRounding.AwayFromZero);

        return (parteVetly, desconto - parteVetly, faixa);
    }

    /// <summary>Percentual que cabe à Vetly na faixa (RN-051), para exibição.</summary>
    public static decimal PercentualVetlyDe(FaixaDeFinanciamento faixa) => faixa switch
    {
        FaixaDeFinanciamento.Ate10 => 100m,
        FaixaDeFinanciamento.De10a30 => 60m,
        _ => 30m
    };
}
