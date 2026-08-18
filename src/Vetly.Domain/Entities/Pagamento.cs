using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um pagamento realizado por um responsavel na plataforma Vetly.
/// Suporta split financeiro entre veterinário autônomo e empresa (RN-Strategy).
/// </summary>
public class Pagamento
{
    /// <summary>Identificador único do pagamento (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id do responsavel que realizou o pagamento. Chave estrangeira para TB_RESPONSAVEL.</summary>
    [Required]
    public Guid ResponsavelId { get; private set; }

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

    /// <summary>Valor estornado ao responsavel em caso de cancelamento. Nulo se não houve estorno.</summary>
    public decimal? ValorEstornado { get; private set; }

    /// <summary>Sempre true no MVP — nenhum valor real transita, tudo é registrado (RN-037).</summary>
    public bool Simulado { get; private set; }

    /// <summary>Percentual de comissão retido pela plataforma, conforme o plano do veterinário (RN-089).</summary>
    public decimal PercentualComissao { get; private set; }

    /// <summary>Valor da comissão retida pela plataforma (Valor × PercentualComissao).</summary>
    public decimal ValorComissao { get; private set; }

    /// <summary>Valor a ser repassado (Valor − ValorComissao) — ao veterinário autônomo ou à empresa.</summary>
    public decimal ValorRepasse { get; private set; }

    /// <summary>
    /// Valor do desconto de fidelidade calculado e exibido, sem abatimento real (RN-072).
    /// Default 0 — preenchido pela Fase 10 (fidelidade).
    /// </summary>
    public decimal DescontoFidelidadeCalculado { get; private set; }

    /// <summary>Parcela do desconto de fidelidade absorvida pela Vetly. Preenchido pela Fase 10.</summary>
    public decimal IncidenciaVetly { get; private set; }

    /// <summary>Parcela do desconto de fidelidade absorvida pelo veterinário. Preenchido pela Fase 10.</summary>
    public decimal IncidenciaVeterinario { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Pagamento() { }

    /// <summary>Cria um novo pagamento com status inicial Pendente. Sempre simulado no MVP (RN-037).</summary>
    public Pagamento(Guid responsavelId, decimal valor, MeioPagamento meio, Guid? consultaId = null, Guid? internacaoId = null)
    {
        Id = Guid.NewGuid();
        ResponsavelId = responsavelId;
        Valor = valor;
        MeioPagamento = meio;
        ConsultaId = consultaId;
        InternacaoId = internacaoId;
        Momento = DateTime.UtcNow;
        StatusPagamento = StatusPagamento.Pendente; // toda transação começa pendente
        Simulado = true;
    }

    /// <summary>Confirma o recebimento do pagamento.</summary>
    public void Confirmar() => StatusPagamento = StatusPagamento.Confirmado;

    /// <summary>
    /// Vincula este pagamento a uma consulta apos o agendamento.
    /// Necessario para que CancelarAsync encontre o pagamento via ObterPorConsultaAsync (RN-019/020/021).
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
    /// Registra a comissão da plataforma conforme o plano do veterinário (RN-089).
    /// ValorComissao e ValorRepasse são recalculados a partir de Valor.
    /// </summary>
    public void RegistrarComissao(decimal percentualComissao)
    {
        PercentualComissao = percentualComissao;
        ValorComissao = Math.Round(Valor * percentualComissao / 100m, 2);
        ValorRepasse = Valor - ValorComissao;
    }

    /// <summary>
    /// Registra o desconto de fidelidade calculado e sua divisão de incidência (RN-072).
    /// Só calculado/exibido — sem abatimento real do valor, já que o pagamento é simulado.
    /// </summary>
    public void RegistrarDescontoFidelidade(decimal descontoCalculado, decimal incidenciaVetly, decimal incidenciaVeterinario)
    {
        DescontoFidelidadeCalculado = descontoCalculado;
        IncidenciaVetly = incidenciaVetly;
        IncidenciaVeterinario = incidenciaVeterinario;
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
