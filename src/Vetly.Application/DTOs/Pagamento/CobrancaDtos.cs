using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Pagamento;

/// <summary>
/// Cobrança criada: o split já apurado e as instruções de pagamento (RN-006/RN-070).
///
/// O status volta como <c>Pendente</c>. A confirmação chega pelo webhook, nunca por
/// esta resposta — é o que mantém o fluxo pronto para um gateway real
/// (vetly-tech §7.5).
/// </summary>
public class CobrancaCriadaRespostaDto
{
    public Guid Id { get; set; }

    public StatusPagamento StatusPagamento { get; set; }

    /// <summary>Valor bruto do serviço, antes de qualquer desconto.</summary>
    public decimal Valor { get; set; }

    /// <summary>O que o Responsável de fato paga, já com o desconto do resgate.</summary>
    public decimal ValorCobrado { get; set; }

    /// <summary>
    /// Desconto de fidelidade aplicado, com a divisão do custo (RN-051). Nulo quando
    /// não houve cupom.
    /// </summary>
    public DescontoDeFidelidadeDto? DescontoFidelidade { get; set; }

    /// <summary>Repartição apurada da transação (RN-070).</summary>
    public SplitDto Split { get; set; } = new();

    /// <summary>
    /// Sempre <c>Simulada</c> no MVP: valores são apurados e registrados, nunca
    /// repassados (RN-071).
    /// </summary>
    public string Liquidacao { get; set; } = "Simulada";

    /// <summary>Como pagar, no formato que o provedor devolveu.</summary>
    public InstrucoesDePagamentoDto Instrucoes { get; set; } = new();
}

/// <summary>
/// Desconto de fidelidade aplicado à cobrança e como o custo foi repartido (RN-051).
///
/// As duas partes aparecem separadas porque saem de bolsos diferentes: a da Vetly
/// reduz a comissão, a do prestador reduz o repasse. Mostrar só o total esconderia
/// quem pagou pela promoção.
/// </summary>
public class DescontoDeFidelidadeDto
{
    public Guid CupomId { get; set; }
    public int PontosResgatados { get; set; }

    /// <summary>Valor do desconto em reais (RN-049).</summary>
    public decimal Valor { get; set; }

    /// <summary>Faixa que definiu a divisão: até R$ 10 · R$ 10,01–30 · acima de R$ 30.</summary>
    public Domain.Enums.FaixaDeFinanciamento? Faixa { get; set; }

    public decimal AbsorvidoVetly { get; set; }
    public decimal AbsorvidoPrestador { get; set; }
}

/// <summary>Repartição da transação entre plataforma e prestador (RN-070/RN-072).</summary>
public class SplitDto
{
    public PlanoAssinatura Plano { get; set; }
    public decimal TakeRate { get; set; }
    public decimal ComissaoVetly { get; set; }
    public decimal Repasse { get; set; }

    /// <summary>Quem recebe o repasse: o vet autônomo ou a clínica (RN-072).</summary>
    public Guid DestinatarioRepasseId { get; set; }
}

/// <summary>Instruções de pagamento devolvidas pelo provedor.</summary>
public class InstrucoesDePagamentoDto
{
    /// <summary>Tipo da instrução (ex: <c>PixSimulado</c>).</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Código a apresentar ao pagador.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Referência da cobrança no provedor.</summary>
    public string ReferenciaExterna { get; set; } = string.Empty;
}

/// <summary>Status da cobrança, para o app fazer polling durante o checkout (RN-006).</summary>
public class StatusDaCobrancaDto
{
    public Guid PagamentoId { get; set; }
    public StatusPagamento StatusPagamento { get; set; }

    /// <summary>Consulta vinculada, quando houver.</summary>
    public Guid? ConsultaId { get; set; }

    /// <summary>Estado da consulta — é o que o app espera virar <c>Confirmada</c>.</summary>
    public StatusConsulta? StatusConsulta { get; set; }

    /// <summary>Verdadeiro quando ainda cabe mudança de status.</summary>
    public bool AguardandoConfirmacao { get; set; }
}

/// <summary>
/// Evento de mudança de status vindo do provedor (§3.6). Estado autoritativo do
/// pagamento — a consulta reage a ele, não ao retorno da criação da cobrança.
/// </summary>
public class WebhookPagamentoDto
{
    [Required(ErrorMessage = "A referência externa é obrigatória.")]
    public string ReferenciaExterna { get; set; } = string.Empty;

    [Required(ErrorMessage = "O status é obrigatório.")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>O que o webhook produziu, para log e diagnóstico.</summary>
public class ResultadoDoWebhookDto
{
    public Guid? PagamentoId { get; set; }
    public StatusPagamento StatusPagamento { get; set; }
    public Guid? ConsultaId { get; set; }
    public StatusConsulta? StatusConsulta { get; set; }

    /// <summary>
    /// Verdadeiro quando o evento não mudou nada — reentrega de um evento já
    /// processado. Webhook é entregue mais de uma vez por natureza.
    /// </summary>
    public bool Ignorado { get; set; }
}
