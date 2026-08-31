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
    /// Indica liquidação financeira efetiva. <b>Sempre falso no MVP</b>: valores são
    /// apurados e registrados, nunca repassados (RN-071).
    /// </summary>
    public bool Liquidado { get; private set; }

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
        if (comissao + repasse != Valor)
            throw new ArgumentException("A soma de comissão e repasse deve fechar o valor da transação.");

        PlanoAplicado = plano;
        TakeRate = takeRate;
        Comissao = comissao;
        Repasse = repasse;
        DestinatarioRepasseId = destinatarioId;

        // PERCENTUAL_SPLIT continua alimentado: e o percentual que fica com o prestador
        PercentualSplit = 100m - takeRate;
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
