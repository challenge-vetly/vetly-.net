namespace Vetly.Application.DTOs.IA;

/// <summary>
/// Prontuário estruturado pela IA a partir da transcrição da consulta (RN-080).
///
/// É sugestão, não conteúdo clínico: só vira documento com a decisão do veterinário
/// (RN-082).
/// </summary>
public class ConsultaEstruturadaDto
{
    /// <summary>Queixa e histórico relatados no atendimento.</summary>
    public string Anamnese { get; set; } = string.Empty;

    /// <summary>Achados do exame físico.</summary>
    public string ExameFisico { get; set; } = string.Empty;

    /// <summary>Hipóteses levantadas, da mais provável à menos.</summary>
    public List<string> HipotesesDiagnosticas { get; set; } = [];

    /// <summary>Conduta e protocolo propostos.</summary>
    public string Conduta { get; set; } = string.Empty;

    /// <summary>Orientações ao Responsável, em linguagem acessível.</summary>
    public string Orientacoes { get; set; } = string.Empty;
}

/// <summary>
/// Contexto entregue à IA para estruturar a consulta (RN-080).
///
/// O texto vem da transcrição; os dados do animal entram porque mudam a leitura
/// clínica do que foi dito — e porque a ausência de peso precisa aparecer no
/// rascunho, e não só na hora de prescrever (RN-081).
/// </summary>
public class ContextoDaEstruturacaoDto
{
    /// <summary>Transcrição da consulta, já na ordem dos trechos.</summary>
    public string Transcricao { get; set; } = string.Empty;

    public string Especie { get; set; } = string.Empty;
    public string Raca { get; set; } = string.Empty;
    public int IdadeAnos { get; set; }

    /// <summary>Nulo quando o peso não está cadastrado (RN-081).</summary>
    public decimal? PesoKg { get; set; }

    public string? Sexo { get; set; }

    /// <summary>Alergias e condições já conhecidas do animal.</summary>
    public List<string> Alergias { get; set; } = [];
    public List<string> CondicoesPreexistentes { get; set; } = [];

    /// <summary>
    /// Verdadeiro quando parte do áudio não foi transcrita. A IA precisa saber que o
    /// relato está incompleto para não preencher lacunas por conta própria (§7.3).
    /// </summary>
    public bool TranscricaoParcial { get; set; }
}
