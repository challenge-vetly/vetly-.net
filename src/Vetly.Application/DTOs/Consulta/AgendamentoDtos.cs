using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>
/// Pré-sintomas informados pelo Responsável no agendamento (RN-005/RN-036).
///
/// É texto guiado, e não campo livre: perguntas fechadas produzem contexto que o
/// veterinário consegue ler em dez segundos no briefing, e que a IA consegue usar.
/// Um parágrafo solto não faz nem uma coisa nem outra.
/// </summary>
public class PreSintomasDto
{
    [Required(ErrorMessage = "A queixa principal é obrigatória.")]
    [MaxLength(300, ErrorMessage = "A queixa deve ter no máximo 300 caracteres.")]
    public string QueixaPrincipal { get; set; } = string.Empty;

    /// <summary>Há quantos dias o quadro começou. Zero para "começou hoje".</summary>
    [Range(0, 3650, ErrorMessage = "A duração deve estar entre 0 e 3650 dias.")]
    public int DuracaoEmDias { get; set; }

    /// <summary>Sinais observados pelo Responsável, na linguagem dele.</summary>
    public List<string> SinaisObservados { get; set; } = [];

    /// <summary>Se o animal está comendo e bebendo normalmente.</summary>
    public bool? AlimentacaoNormal { get; set; }

    /// <summary>Se houve mudança recente de comportamento.</summary>
    public bool? MudancaDeComportamento { get; set; }

    [MaxLength(1000, ErrorMessage = "As observações devem ter no máximo 1000 caracteres.")]
    public string? Observacoes { get; set; }

    /// <summary>Fotos ou vídeos anexados, já enviados ao storage (§2.6).</summary>
    public List<Guid> MidiaIds { get; set; } = [];
}

/// <summary>Pedido de remarcação (RN-013/RN-043).</summary>
public class RemarcarConsultaDto
{
    [Required(ErrorMessage = "O novo horário é obrigatório.")]
    public Guid NovoSlotId { get; set; }
}

/// <summary>Resultado da remarcação (RN-013/RN-043).</summary>
public class RemarcacaoRealizadaDto
{
    public Guid ConsultaId { get; set; }
    public DateTime HorarioAnterior { get; set; }
    public DateTime NovoHorario { get; set; }

    /// <summary>Quantas remarcações já foram usadas nesta consulta.</summary>
    public int Remarcacoes { get; set; }

    /// <summary>Quantas ainda cabem. Zero significa que só resta cancelar (RN-043).</summary>
    public int RemarcacoesRestantes { get; set; }

    /// <summary>
    /// O pagamento é transferido para a nova data, sem nova cobrança (RN-013).
    /// </summary>
    public StatusPagamento StatusPagamento { get; set; }
}

/// <summary>
/// O que aconteceria se a consulta fosse cancelada agora (RN-014/RN-041/RN-042).
///
/// Existe separado do cancelamento porque o Responsável precisa ver o valor
/// <b>antes</b> de confirmar. Descobrir a retenção depois de cancelar é descobrir
/// tarde demais.
/// </summary>
public class SimulacaoDeCancelamentoDto
{
    public Guid ConsultaId { get; set; }

    /// <summary>Qual das três janelas da RN-041 se aplica agora.</summary>
    public string EstrategiaAplicada { get; set; } = string.Empty;

    public double HorasDeAntecedencia { get; set; }

    public decimal ValorPago { get; set; }

    /// <summary>Percentual retido pela clínica na faixa parcial (RN-042).</summary>
    public decimal PercentualRetencao { get; set; }

    public decimal ValorRetido { get; set; }
    public decimal ValorReembolso { get; set; }

    /// <summary>
    /// Sempre <c>Simulada</c> no MVP: o valor é calculado e exibido, não liquidado
    /// (RN-041).
    /// </summary>
    public string Liquidacao { get; set; } = "Simulada";
}

/// <summary>Registro de não comparecimento (RN-044).</summary>
public class NoShowRegistradoDto
{
    public Guid ConsultaId { get; set; }
    public StatusConsulta Status { get; set; }

    /// <summary>
    /// Sempre falso: o no-show segue a faixa "menos de 2h ou no ato" da RN-014, que
    /// não reembolsa.
    /// </summary>
    public bool GerouReembolso { get; set; }

    public DateTime RegistradoEm { get; set; }
}
