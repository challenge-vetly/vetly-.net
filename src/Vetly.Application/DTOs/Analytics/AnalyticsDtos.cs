namespace Vetly.Application.DTOs.Analytics;

/// <summary>
/// Métricas da plataforma num período (RN-106).
///
/// São três perguntas, e as seções respondem uma cada: o agendamento está virando
/// atendimento? o dinheiro está entrando? a IA está ajudando ou dando trabalho?
///
/// Nenhum número aqui identifica pessoa: analytics é agregado, e cruzar métrica com
/// dado de Responsável ou de animal seria usar a base clínica para outra coisa.
/// </summary>
public class AnalyticsDaPlataformaDto
{
    public DateTime PeriodoInicio { get; set; }
    public DateTime PeriodoFim { get; set; }

    /// <summary>Do agendamento ao atendimento (RN-035/RN-038).</summary>
    public FunilDeAtendimentoDto Funil { get; set; } = new();

    /// <summary>Como a IA está sendo recebida pelos veterinários (RN-082).</summary>
    public UsoDaIaDto Ia { get; set; } = new();

    /// <summary>Volume e valor do que foi cobrado (RN-070).</summary>
    public ReceitaDoPeriodoDto Receita { get; set; } = new();
}

/// <summary>
/// O caminho do agendamento até o atendimento (RN-035/RN-038).
///
/// As taxas importam mais que os absolutos: 30 cancelamentos em 1000 consultas é
/// ruído; em 60, é um problema de agenda.
/// </summary>
public class FunilDeAtendimentoDto
{
    /// <summary>Consultas criadas no período, por qualquer caminho.</summary>
    public int Criadas { get; set; }

    /// <summary>Chegaram a ser confirmadas pelo pagamento.</summary>
    public int Confirmadas { get; set; }

    /// <summary>Aconteceram de fato.</summary>
    public int Realizadas { get; set; }

    public int Canceladas { get; set; }

    /// <summary>Responsável não compareceu (RN-044).</summary>
    public int NoShow { get; set; }

    /// <summary>Checkout que venceu sem pagamento (RN-035).</summary>
    public int Expiradas { get; set; }

    /// <summary>Percentual das criadas que virou atendimento, de 0 a 100.</summary>
    public decimal TaxaDeConversao { get; set; }

    /// <summary>Percentual das confirmadas que foi cancelado, de 0 a 100.</summary>
    public decimal TaxaDeCancelamento { get; set; }

    /// <summary>Percentual das confirmadas que virou não comparecimento, de 0 a 100.</summary>
    public decimal TaxaDeNoShow { get; set; }
}

/// <summary>
/// Como a IA está sendo recebida (RN-082).
///
/// A métrica que interessa não é quantos rascunhos foram gerados: é quantos o
/// veterinário aceitou <b>sem corrigir</b>. Correção alta significa que a IA está
/// dando trabalho em vez de poupar, e recusa alta significa que ela está errando o
/// suficiente para não ser confiável.
/// </summary>
public class UsoDaIaDto
{
    /// <summary>Decisões registradas na trilha de auditoria no período.</summary>
    public int DecisoesRegistradas { get; set; }

    public int Aprovados { get; set; }
    public int Corrigidos { get; set; }
    public int NaoAprovados { get; set; }

    /// <summary>Prontuários escritos à mão, sem IA no caminho (RN-085).</summary>
    public int ProntuariosManuais { get; set; }

    /// <summary>Percentual de rascunhos aceitos sem alteração, de 0 a 100.</summary>
    public decimal TaxaDeAprovacaoSemCorrecao { get; set; }

    /// <summary>Percentual de rascunhos recusados, de 0 a 100.</summary>
    public decimal TaxaDeRecusa { get; set; }
}

/// <summary>Volume e valor do que foi cobrado no período (RN-070).</summary>
public class ReceitaDoPeriodoDto
{
    public int TransacoesConfirmadas { get; set; }
    public decimal ValorBruto { get; set; }
    public decimal ComissaoDaPlataforma { get; set; }

    /// <summary>Valor médio por atendimento cobrado.</summary>
    public decimal TicketMedio { get; set; }

    /// <summary>Percentual médio efetivamente retido, de 0 a 100.</summary>
    public decimal TakeRateEfetivo { get; set; }
}
