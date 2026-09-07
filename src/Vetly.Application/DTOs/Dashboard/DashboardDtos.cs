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

    /// <summary>
    /// Responsáveis que não responderam à régua de lembretes (RN-095, §6.4).
    ///
    /// A régua já gravava o alerta depois de três tentativas sem resposta; o que não
    /// existia era onde lê-lo. Aparece aqui porque §6.4 diz "alerta no dashboard", e
    /// só dos animais que este profissional atendeu — a régua é do animal, mas quem
    /// tem contexto para agir é quem já o viu.
    /// </summary>
    public List<AlertaDeReguaDto> ResponsaveisNaoResponsivos { get; set; } = [];
}

/// <summary>
/// Um Responsável que parou de responder sobre um cuidado do animal (RN-095, §6.4).
///
/// O alerta não pede notificação nova: a régua já tentou três vezes, e insistir seria
/// fadiga. Pede que alguém da clínica ligue — e é por isso que a linha traz o animal
/// e o assunto, não só um contador.
/// </summary>
public class AlertaDeReguaDto
{
    public Guid LembreteId { get; set; }
    public Guid AnimalId { get; set; }
    public string AnimalNome { get; set; } = string.Empty;
    public Guid TutorId { get; set; }

    /// <summary>Assunto da régua — vacina, vermífugo, retorno, medicação ou check-up.</summary>
    public Domain.Enums.TipoLembrete Tipo { get; set; }

    /// <summary>Data do cuidado que ficou sem resposta.</summary>
    public DateTime DataEvento { get; set; }

    /// <summary>Quantas tentativas a régua fez antes de escalar. Três, por construção.</summary>
    public int TentativasRealizadas { get; set; }
}

/// <summary>
/// Painel consolidado da unidade para o administrador (§5.2, §7.3).
///
/// É o mesmo dia do painel do veterinário, visto de cima: a agenda de todos os
/// profissionais e os indicadores operacionais do estabelecimento. O que <b>não</b>
/// está aqui é tão deliberado quanto o que está — a §7.3 veda ao administrador dados
/// bancários pessoais dos vinculados e remuneração interna, então o painel mostra
/// produção, nunca "salário". O consolidado financeiro tem rota própria
/// (<c>GET /api/financeiro/consolidado</c>) e é lá que o dinheiro aparece.
/// </summary>
public class DashboardDaUnidadeDto
{
    public Guid EmpresaId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Referência do painel — o dia que a unidade está enxergando.</summary>
    public DateTime Data { get; set; }

    /// <summary>Um bloco por veterinário vinculado, com a agenda do dia de cada um.</summary>
    public List<AgendaDoProfissionalDto> Profissionais { get; set; } = [];

    /// <summary>Indicadores operacionais do dia (§5.2).</summary>
    public IndicadoresDaUnidadeDto Indicadores { get; set; } = new();

    /// <summary>Responsáveis não responsivos dos animais atendidos pela unidade (RN-095).</summary>
    public List<AlertaDeReguaDto> ResponsaveisNaoResponsivos { get; set; } = [];
}

/// <summary>A agenda do dia de um profissional da unidade (§5.2).</summary>
public class AgendaDoProfissionalDto
{
    public Guid VeterinarioId { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Falso para quem foi desativado. O profissional continua no painel porque a
    /// agenda dele pode ter atendimentos futuros a redistribuir (RN-025) — sumir com
    /// ele esconderia exatamente o que o administrador precisa resolver.
    /// </summary>
    public bool Ativo { get; set; }

    /// <summary>Horários materializados para o dia. Zero significa agenda não configurada.</summary>
    public int HorariosNoDia { get; set; }

    /// <summary>Quantos desses horários estão ocupados.</summary>
    public int HorariosOcupados { get; set; }

    public List<AtendimentoDoDiaDto> AgendaDeHoje { get; set; } = [];
}

/// <summary>
/// Indicadores operacionais do dia da unidade (§5.2, §7.3).
///
/// Operacionais, e não financeiros: quantos atendimentos, quantos furaram, quanto da
/// agenda foi usada. Faturamento, comissão e repasse vivem no consolidado financeiro,
/// que tem o recorte de período e as vedações da §7.3 já aplicadas.
/// </summary>
public class IndicadoresDaUnidadeDto
{
    /// <summary>Profissionais vinculados e ativos.</summary>
    public int ProfissionaisAtivos { get; set; }

    /// <summary>Atendimentos do dia que não foram cancelados nem expiraram.</summary>
    public int AtendimentosNoDia { get; set; }

    public int Realizados { get; set; }
    public int Cancelados { get; set; }
    public int NoShow { get; set; }

    /// <summary>Horários materializados no dia, somando todos os profissionais.</summary>
    public int HorariosNoDia { get; set; }

    public int HorariosOcupados { get; set; }

    /// <summary>
    /// Ocupação da agenda em percentual. Zero quando não há horário materializado —
    /// e não 100%, que é o que uma divisão por zero mal tratada produziria e o que
    /// faria uma unidade sem agenda parecer lotada.
    /// </summary>
    public decimal TaxaDeOcupacao { get; set; }
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
