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

    /// <summary>Define o percentual do split financeiro após processamento pela Strategy.</summary>
    public void DefinirSplit(decimal percentual) => PercentualSplit = percentual;

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
