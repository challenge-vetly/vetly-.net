using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Fidelidade;

/// <summary>Saldo e tier do Responsável (RN-047 a RN-050).</summary>
public class SaldoDePontosDto
{
    public Guid TutorId { get; set; }

    /// <summary>Soma dos lançamentos. Não é campo guardado: é o extrato somado.</summary>
    public int Saldo { get; set; }

    /// <summary>Quanto o saldo vale em desconto — 100 pontos = R$ 3,00 (RN-049).</summary>
    public decimal ValorEmReais { get; set; }

    /// <summary>
    /// Pontos que vencem nos próximos 30 dias. Avisar antes é o que separa o programa
    /// de fidelidade de uma pegadinha.
    /// </summary>
    public int PontosVencendoEm30Dias { get; set; }

    /// <summary>Faixa vigente (RN-048).</summary>
    public TierFidelidade Tier { get; set; }

    /// <summary>Multiplicador de ganho do tier: 1,0× · 1,25× · 1,5× (RN-048).</summary>
    public decimal Multiplicador { get; set; }

    /// <summary>Pontos creditados na janela móvel de 12 meses, base do tier.</summary>
    public int AcumuloEm12Meses { get; set; }

    /// <summary>Quanto falta para subir de faixa. Zero no Ouro.</summary>
    public int PontosParaProximoTier { get; set; }
}

/// <summary>Um lançamento no extrato de pontos (RN-047 a RN-052).</summary>
public class MovimentoDePontosDto
{
    public Guid Id { get; set; }
    public TipoMovimentoDePontos Tipo { get; set; }

    /// <summary>Positivo em crédito, negativo em débito, estorno e expiração.</summary>
    public int Pontos { get; set; }

    /// <summary>Pontos antes do multiplicador de tier (RN-048).</summary>
    public int PontosBrutos { get; set; }

    public decimal Multiplicador { get; set; }

    /// <summary>Quanto ainda resta deste lote de crédito, no consumo FIFO (RN-050).</summary>
    public int Restante { get; set; }

    public Guid? ConsultaId { get; set; }
    public Guid? ObrigacaoId { get; set; }
    public Guid? CupomId { get; set; }
    public decimal? ValorEmReais { get; set; }
    public DateTime? ExpiraEm { get; set; }
    public string? Descricao { get; set; }
    public DateTime OcorridoEm { get; set; }
}

/// <summary>
/// Pedido de simulação ou de resgate de pontos (RN-017/RN-018).
///
/// <para>
/// <b>Forma atual:</b> só <see cref="ItemId"/>. O servidor busca o item no catálogo e
/// deriva nome, categoria e pontos (RN-108) — os demais campos, se vierem, são
/// ignorados.
/// </para>
/// <para>
/// <b>Forma legada</b> (<see cref="ItemRef"/>, <see cref="ItemNome"/>,
/// <see cref="Categoria"/>, <see cref="Pontos"/>): continua aceita para não quebrar o
/// app publicado antes da Sprint 4, que ainda não conhece o catálogo. Está
/// <b>depreciada</b> — nela é o cliente quem diz quantos pontos debitar, e cada uso
/// fica registrado em log de aviso para medir quando ela pode sair.
/// </para>
/// </summary>
public class SimularResgateDto : IValidatableObject
{
    /// <summary>
    /// Item do catálogo (<c>GET /api/marketplace/itens</c>). Quando informado, é a única
    /// fonte do resgate (RN-108).
    /// </summary>
    public Guid? ItemId { get; set; }

    /// <summary>
    /// <b>Depreciado</b> — use <see cref="ItemId"/>. Referência do item no catálogo
    /// que o front mantinha (RN-098). Obrigatório só na forma legada.
    /// </summary>
    [MaxLength(120)]
    public string ItemRef { get; set; } = string.Empty;

    /// <summary><b>Depreciado</b> — use <see cref="ItemId"/>. Nome do item na forma legada.</summary>
    [MaxLength(200)]
    public string? ItemNome { get; set; }

    /// <summary><b>Depreciado</b> — use <see cref="ItemId"/>. Categoria na forma legada.</summary>
    public CategoriaItem Categoria { get; set; }

    /// <summary>
    /// <b>Depreciado</b> — use <see cref="ItemId"/>. Pontos a debitar, escolhidos pelo
    /// cliente na forma legada.
    /// </summary>
    public int Pontos { get; set; }

    /// <summary>
    /// As regras da forma legada só valem quando não há <see cref="ItemId"/>. Como
    /// atributos, elas recusariam o pedido novo — que manda só o id — com 400.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ItemId is { } id)
        {
            if (id == Guid.Empty)
                yield return new ValidationResult("Informe o item.", [nameof(ItemId)]);

            yield break;
        }

        // Mensagens da forma legada mantidas como eram: o app publicado pode exibi-las
        if (string.IsNullOrWhiteSpace(ItemRef))
            yield return new ValidationResult("O item é obrigatório.", [nameof(ItemRef)]);

        if (!Enum.IsDefined(Categoria))
            yield return new ValidationResult("A categoria é obrigatória.", [nameof(Categoria)]);

        if (Pontos < 1)
            yield return new ValidationResult("O resgate deve debitar pontos.", [nameof(Pontos)]);
    }
}

/// <summary>Resultado da simulação, exibido antes de confirmar (RN-017/RN-051).</summary>
public class SimulacaoDeResgateDto
{
    /// <summary>Item do catálogo simulado; nulo na forma legada do pedido.</summary>
    public Guid? ItemId { get; set; }

    public string ItemRef { get; set; } = string.Empty;

    /// <summary>Nome do item, como sairá no cupom.</summary>
    public string? ItemNome { get; set; }

    public CategoriaItem Categoria { get; set; }

    public int PontosADebitar { get; set; }

    /// <summary>Desconto em reais (RN-049).</summary>
    public decimal Desconto { get; set; }

    /// <summary>Faixa de financiamento aplicável (RN-051).</summary>
    public FaixaDeFinanciamento Faixa { get; set; }

    public decimal PercentualVetly { get; set; }
    public decimal PercentualPrestador { get; set; }

    /// <summary>Parte do desconto absorvida pela Vetly.</summary>
    public decimal ValorVetly { get; set; }

    /// <summary>Parte do desconto absorvida pelo prestador.</summary>
    public decimal ValorPrestador { get; set; }

    public int ValidadeDias { get; set; }
    public int SaldoApos { get; set; }

    /// <summary>
    /// Sempre <c>Simulado</c> no MVP: a divisão é calculada, gravada e exibida, sem
    /// abatimento financeiro real (RN-051).
    /// </summary>
    public string Abatimento { get; set; } = "Simulado";
}

/// <summary>Cupom emitido no resgate (RN-053/RN-054).</summary>
public class CupomDto
{
    public Guid Id { get; set; }

    /// <summary>Código que o app renderiza como QR.</summary>
    public string CodigoQr { get; set; } = string.Empty;

    public string ItemRef { get; set; } = string.Empty;

    /// <summary>Item do catálogo resgatado; nulo nos cupons anteriores ao catálogo.</summary>
    public Guid? ItemMarketplaceId { get; set; }

    public string? ItemNome { get; set; }
    public CategoriaItem Categoria { get; set; }

    public int PontosDebitados { get; set; }
    public decimal Desconto { get; set; }

    public FaixaDeFinanciamento Faixa { get; set; }
    public decimal DescontoVetly { get; set; }
    public decimal DescontoPrestador { get; set; }

    public StatusCupom Status { get; set; }
    public DateTime EmitidoEm { get; set; }

    /// <summary>Vencido o prazo, os pontos <b>não</b> retornam ao saldo (RN-053).</summary>
    public DateTime ExpiraEm { get; set; }

    public DateTime? ResgatadoEm { get; set; }
}
