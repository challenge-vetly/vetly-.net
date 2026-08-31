using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um pagamento realizado por um tutor na plataforma Vetly.
/// Suporta split financeiro entre veterinário autônomo e empresa (RN-Strategy).
/// </summary>
public class Pagamento
{
    /// <summary>Identificador único do pagamento (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id do tutor que realizou o pagamento. Chave estrangeira para TB_TUTOR.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>
    /// Id da consulta vinculada ao pagamento.
    /// Nulo quando o pagamento é referente a uma internação.
    /// </summary>
    public Guid? ConsultaId { get; private set; }

    /// <summary>
    /// Id da internação vinculada ao pagamento.
    /// Nulo quando o pagamento é referente a uma consulta.
    /// </summary>
    public Guid? InternacaoId { get; private set; }

    /// <summary>Valor total do pagamento em reais.</summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "O valor do pagamento deve ser maior que zero.")]
    public decimal Valor { get; private set; }

    /// <summary>Meio de pagamento utilizado (Pix, Cartão, Dinheiro etc).</summary>
    [Required]
    public MeioPagamento MeioPagamento { get; private set; }

    /// <summary>Data e hora em que o pagamento foi registrado.</summary>
    public DateTime Momento { get; private set; }

    /// <summary>Status atual do pagamento (Pendente, Confirmado, Estornado ou Parcial).</summary>
    [Required]
    public StatusPagamento StatusPagamento { get; private set; }

    /// <summary>
    /// Percentual do valor que será repassado ao veterinário após o split.
    /// 0 quando ainda não processado. Ex: 70 significa 70% para o veterinário.
    /// </summary>
    [Range(0, 100)]
    public decimal PercentualSplit { get; private set; }

    /// <summary>Valor estornado ao tutor em caso de cancelamento. Nulo se não houve estorno.</summary>
    public decimal? ValorEstornado { get; private set; }

    // ── Cobrança (RN-006/RN-071, §5.1) ───────────────────────────────────────

    /// <summary>O que está sendo cobrado (RN-101).</summary>
    public TipoPagamento Tipo { get; private set; }

    /// <summary>Identificador da cobrança no provedor. Nulo antes de criá-la.</summary>
    [MaxLength(100)]
    public string? ReferenciaExterna { get; private set; }

    /// <summary>
    /// Chave de idempotência da transação: reenviar a mesma cobrança não duplica nada
    /// (vetly-tech §7.5).
    /// </summary>
    [MaxLength(100)]
    public string? ChaveIdempotencia { get; private set; }

    /// <summary>
    /// Indica liquidação financeira efetiva. Valores são apurados e registrados, e o
    /// repasse em si acontece fora da plataforma (RN-071) — nada no MVP liga esta
    /// transição sozinho; ela existe para o painel financeiro marcar o que já saiu.
    /// </summary>
    public bool Liquidado { get; private set; }

    // ── Fidelidade (RN-051) ──────────────────────────────────────────────────

    /// <summary>Cupom de resgate aplicado a esta cobrança (RN-053).</summary>
    public Guid? CupomId { get; private set; }

    /// <summary>Pontos de fidelidade que o cupom debitou.</summary>
    public int? PontosResgatados { get; private set; }

    /// <summary>Desconto em reais concedido pelo resgate (RN-049).</summary>
    public decimal? ValorDoDesconto { get; private set; }

    /// <summary>
    /// Parte do desconto absorvida pela Vetly, conforme a faixa da RN-051.
    /// Sai da comissão.
    /// </summary>
    public decimal? DescontoVetly { get; private set; }

    /// <summary>
    /// Parte do desconto absorvida pelo prestador, conforme a faixa da RN-051.
    /// Sai do repasse.
    /// </summary>
    public decimal? DescontoPrestador { get; private set; }

    /// <summary>Faixa de financiamento aplicada ao desconto (RN-051).</summary>
    public FaixaDeFinanciamento? FaixaDoDesconto { get; private set; }

    /// <summary>
    /// O que o Responsável de fato paga: o valor bruto menos o desconto do resgate.
    /// </summary>
    public decimal ValorCobrado => Valor - (ValorDoDesconto ?? 0m);

    // ── Split por plano (RN-070/RN-072) ──────────────────────────────────────

    /// <summary>Plano que definiu o take rate desta transação (RN-070).</summary>
    public PlanoAssinatura? PlanoAplicado { get; private set; }

    /// <summary>Percentual retido pela Vetly, de 0 a 100.</summary>
    public decimal? TakeRate { get; private set; }

    /// <summary>Valor retido pela Vetly. Registrado, não liquidado, no MVP (RN-071).</summary>
    public decimal? Comissao { get; private set; }

    /// <summary>Valor repassado ao prestador. Registrado, não liquidado (RN-071).</summary>
    public decimal? Repasse { get; private set; }

    /// <summary>
    /// Quem recebe o repasse: o veterinário autônomo ou a empresa. A remuneração
    /// interna dos vinculados é relação da clínica, fora do escopo (RN-072).
    /// </summary>
    public Guid? DestinatarioRepasseId { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Pagamento() { }

    /// <summary>Cria um novo pagamento com status inicial Pendente.</summary>
    public Pagamento(Guid tutorId, decimal valor, MeioPagamento meio, Guid? consultaId = null, Guid? internacaoId = null)
    {
        Id = Guid.NewGuid();
        TutorId = tutorId;
        Valor = valor;
        MeioPagamento = meio;
        ConsultaId = consultaId;
        InternacaoId = internacaoId;
        Momento = DateTime.UtcNow;
        StatusPagamento = StatusPagamento.Pendente; // toda transação começa pendente
    }

    /// <summary>Confirma o recebimento do pagamento.</summary>
    public void Confirmar() => StatusPagamento = StatusPagamento.Confirmado;

    /// <summary>Define o que está sendo cobrado (RN-101).</summary>
    public void DefinirTipo(TipoPagamento tipo) => Tipo = tipo;

    /// <summary>
    /// Registra a cobrança criada no provedor. O pagamento continua pendente: quem
    /// confirma é o webhook, nunca a resposta síncrona (vetly-tech §7.5).
    /// </summary>
    public void RegistrarCobranca(string referenciaExterna, string chaveIdempotencia)
    {
        if (string.IsNullOrWhiteSpace(referenciaExterna))
            throw new ArgumentException("A referência externa é obrigatória.", nameof(referenciaExterna));

        ReferenciaExterna = referenciaExterna;
        ChaveIdempotencia = chaveIdempotencia;
    }

    /// <summary>
    /// Registra a recusa do provedor. O horário travado deve voltar a ficar livre e a
    /// consulta expira (RN-006/RN-035).
    /// </summary>
    public void Recusar() => StatusPagamento = StatusPagamento.Recusado;

    /// <summary>Verdadeiro quando o pagamento já teve desfecho — não cabe mais mudar.</summary>
    public bool TemDesfecho() =>
        StatusPagamento is StatusPagamento.Confirmado or StatusPagamento.Recusado
            or StatusPagamento.Estornado or StatusPagamento.Parcial;

    /// <summary>
    /// Vincula este pagamento a uma consulta apos o agendamento.
    /// Necessario para que CancelarAsync encontre o pagamento via ObterPorConsultaAsync (RN-014/RN-041/RN-042).
    /// </summary>
    public void VincularConsulta(Guid consultaId)
    {
        if (ConsultaId.HasValue && ConsultaId.Value != consultaId)
            throw new InvalidOperationException("Pagamento ja esta vinculado a outra consulta.");
        ConsultaId = consultaId;
    }

    /// <summary>Define o percentual do split financeiro após processamento pela Strategy.</summary>
    public void DefinirSplit(decimal percentual) => PercentualSplit = percentual;

    /// <summary>
    /// Registra a repartição da transação (RN-070/RN-071). No MVP os valores são
    /// apurados e gravados, nunca liquidados.
    /// </summary>
    public void RegistrarSplit(
        PlanoAssinatura plano, decimal takeRate, decimal comissao, decimal repasse, Guid destinatarioId)
    {
        // Com resgate, o desconto e a terceira parcela do bruto: ele sai da comissao,
        // mas continua sendo dinheiro que existiu no preco do servico (RN-051).
        if (comissao + repasse + (ValorDoDesconto ?? 0m) != Valor)
            throw new ArgumentException(
                "A soma de comissão, repasse e desconto deve fechar o valor da transação.");

        PlanoAplicado = plano;
        TakeRate = takeRate;
        Comissao = comissao;
        Repasse = repasse;
        DestinatarioRepasseId = destinatarioId;

        // PERCENTUAL_SPLIT continua alimentado: e o percentual que fica com o prestador
        PercentualSplit = 100m - takeRate;
    }

    /// <summary>
    /// Aplica o desconto de um cupom de fidelidade (RN-051).
    ///
    /// O desconto NAO reduz <see cref="Valor"/>: o bruto continua sendo o preco do
    /// servico. O que o Responsavel paga e <see cref="ValorCobrado"/>, e o custo do
    /// desconto e <b>repartido entre Vetly e prestador pela faixa da RN-051</b> —
    /// ate R$ 10 a Vetly banca sozinha; acima disso o prestador participa, porque e
    /// ele quem captura a recorrencia que o resgate grande representa.
    ///
    /// Guardar as duas partes separadas, e nao so o total, e o que permite ao
    /// consolidado financeiro dizer de qual bolso saiu cada centavo.
    /// </summary>
    public void AplicarDesconto(
        Guid cupomId,
        int pontosResgatados,
        decimal valorDoDesconto,
        decimal descontoVetly,
        decimal descontoPrestador,
        FaixaDeFinanciamento faixa)
    {
        if (pontosResgatados <= 0 || valorDoDesconto <= 0)
            throw new ArgumentOutOfRangeException(nameof(valorDoDesconto),
                "O desconto deve ser maior que zero.");

        if (valorDoDesconto > Valor)
            throw new ArgumentOutOfRangeException(nameof(valorDoDesconto),
                "O desconto não pode passar do valor da cobrança.");

        if (descontoVetly + descontoPrestador != valorDoDesconto)
            throw new ArgumentException(
                "As partes da incidência devem fechar o valor do desconto.", nameof(descontoVetly));

        CupomId = cupomId;
        PontosResgatados = pontosResgatados;
        ValorDoDesconto = valorDoDesconto;
        DescontoVetly = descontoVetly;
        DescontoPrestador = descontoPrestador;
        FaixaDoDesconto = faixa;
    }

    /// <summary>
    /// Marca o repasse como liquidado — o dinheiro saiu para o prestador (RN-072).
    ///
    /// Só pagamento confirmado liquida: marcar como pago um repasse cuja cobrança não
    /// se confirmou faria o extrato do profissional mentir justamente no número que
    /// ele vem conferir (RN-024).
    ///
    /// A rota que dispara a liquidação em lote é do painel financeiro (onda 8); aqui
    /// fica só a transição, que é o que o extrato precisa saber.
    /// </summary>
    public void Liquidar()
    {
        if (StatusPagamento != StatusPagamento.Confirmado)
            throw new InvalidOperationException("Somente pagamento confirmado pode ser liquidado.");

        Liquidado = true;
    }

    /// <summary>
    /// Registra o estorno total ou parcial do pagamento.
    /// Estorno igual ou maior que o valor original define status como Estornado;
    /// estorno parcial define como Parcial.
    /// </summary>
    public void Estornar(decimal valor)
    {
        ValorEstornado = valor;
        StatusPagamento = valor >= Valor ? StatusPagamento.Estornado : StatusPagamento.Parcial;
    }
}
