namespace Vetly.Application.DTOs.Dashboard;

/// <summary>
/// Painel do veterinário: o que precisa da atenção dele agora (RN-024/RN-105).
///
/// Não é relatório. A ordem das seções segue a ordem em que as coisas travam: o que
/// está atrasado no ciclo de documentação bloqueia o pagamento, o que está agendado
/// para hoje define o dia, e os números do mês são contexto.
/// </summary>
public class DashboardDoVeterinarioDto
{
    public Guid VeterinarioId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Referência do painel — o dia que ele está enxergando.</summary>
    public DateTime Data { get; set; }

    /// <summary>Atendimentos de hoje, do próximo ao último.</summary>
    public List<AtendimentoDoDiaDto> AgendaDeHoje { get; set; } = [];

    /// <summary>O que está parado esperando ação do veterinário.</summary>
    public PendenciasDoVeterinarioDto Pendencias { get; set; } = new();

    /// <summary>Números do mês corrente.</summary>
    public ResumoDoMesDto Mes { get; set; } = new();

    /// <summary>Reputação atual (RN-057).</summary>
    public decimal NotaMedia { get; set; }
    public int NumAvaliacoes { get; set; }

    /// <summary>Falso enquanto a nota não tem avaliações suficientes para valer.</summary>
    public bool NotaPublica { get; set; }
}

/// <summary>Um atendimento na agenda do dia.</summary>
public class AtendimentoDoDiaDto
{
    public Guid ConsultaId { get; set; }
    public DateTime DataHora { get; set; }
    public Guid AnimalId { get; set; }
    public string AnimalNome { get; set; } = string.Empty;
    public string Especie { get; set; } = string.Empty;

    /// <summary>Estado da consulta — o que separa "vai acontecer" de "já aconteceu".</summary>
    public Domain.Enums.StatusConsulta Status { get; set; }

    public Domain.Enums.ModalidadeAtendimento Modalidade { get; set; }

    /// <summary>
    /// Verdadeiro quando o animal não tem peso cadastrado. Aparece no painel porque
    /// sem peso não há sugestão de dose, e descobrir isso durante a consulta é tarde
    /// (RN-081).
    /// </summary>
    public bool PesoAusente { get; set; }
}

/// <summary>
/// O que está esperando ação do veterinário.
///
/// São as três coisas que travam dinheiro ou documento: rascunho sem decisão não gera
/// documento, documento sem assinatura não fecha a consulta, e consulta sem encerrar
/// não gera nada.
/// </summary>
public class PendenciasDoVeterinarioDto
{
    /// <summary>Consultas iniciadas que nunca foram encerradas (RN-008).</summary>
    public int ConsultasNaoEncerradas { get; set; }

    /// <summary>Rascunhos de IA aguardando a decisão do veterinário (RN-082).</summary>
    public int RascunhosAguardandoDecisao { get; set; }

    /// <summary>Documentos emitidos que exigem assinatura e ainda não a têm (RN-087).</summary>
    public int DocumentosAguardandoAssinatura { get; set; }

    /// <summary>Avaliações recebidas que ainda não foram respondidas (RN-055).</summary>
    public int AvaliacoesSemResposta { get; set; }

    /// <summary>Verdadeiro quando há qualquer coisa parada esperando ele.</summary>
    public bool TemPendencia { get; set; }
}

/// <summary>Números do mês corrente para o veterinário (RN-070/RN-072).</summary>
public class ResumoDoMesDto
{
    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }

    /// <summary>Atendimentos realizados e cobrados no período.</summary>
    public int AtendimentosRealizados { get; set; }

    /// <summary>Atendimentos cancelados — o número que revela problema de agenda.</summary>
    public int Cancelamentos { get; set; }

    /// <summary>Soma cobrada dos Responsáveis.</summary>
    public decimal ValorBruto { get; set; }

    /// <summary>O que cabe ao prestador (RN-072).</summary>
    public decimal RepasseApurado { get; set; }

    /// <summary>Parte do repasse ainda não liquidada.</summary>
    public decimal RepassePendente { get; set; }
}
