using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Prontuário estruturado pela IA a partir da transcrição da consulta (RN-080, §7.3).
///
/// É <b>rascunho</b>, e a palavra é literal: nada aqui vira documento sem a decisão
/// explícita do veterinário (RN-082). Guardar o texto de origem, o modelo e o momento
/// da geração é o que permite, depois, responder de onde veio cada frase — sem isso
/// não há como auditar uma sugestão que chegou ao prontuário.
/// </summary>
public class RascunhoIa
{
    /// <summary>Identificador do rascunho (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Sessão de captura que originou o rascunho. Há no máximo um por sessão.</summary>
    [Required]
    public Guid SessaoCapturaId { get; private set; }

    /// <summary>Consulta a que o rascunho pertence.</summary>
    [Required]
    public Guid ConsultaId { get; private set; }

    /// <summary>Queixa e histórico relatados no atendimento.</summary>
    public string Anamnese { get; private set; }

    /// <summary>Achados do exame físico.</summary>
    public string ExameFisico { get; private set; }

    /// <summary>Hipóteses levantadas, da mais provável à menos.</summary>
    public List<string> HipotesesDiagnosticas { get; private set; }

    /// <summary>Conduta e protocolo propostos.</summary>
    public string Conduta { get; private set; }

    /// <summary>Orientações ao Responsável, em linguagem acessível.</summary>
    public string Orientacoes { get; private set; }

    /// <summary>
    /// Texto que alimentou a estruturação. Fica junto do rascunho de propósito: é a
    /// única forma de conferir depois se a IA inventou algo que não foi dito.
    /// </summary>
    public string TextoOrigem { get; private set; }

    /// <summary>Modelo e versão que produziram o rascunho — parte da trilha de auditoria.</summary>
    [MaxLength(100)]
    public string? Modelo { get; private set; }

    /// <summary>
    /// Verdadeiro quando o rascunho saiu de uma transcrição incompleta (§7.3). O
    /// veterinário precisa saber que faltou áudio antes de aprovar.
    /// </summary>
    public bool Parcial { get; private set; }

    /// <summary>
    /// Avisos que acompanham o rascunho. Ex.: <c>TranscricaoParcial</c>,
    /// <c>PesoAusente</c> (RN-081).
    /// </summary>
    public List<string> Avisos { get; private set; }

    public DateTime GeradoEm { get; private set; }

    /// <summary>Quanto a estruturação demorou, em milissegundos.</summary>
    public int DuracaoMs { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private RascunhoIa()
    {
        Anamnese = null!;
        ExameFisico = null!;
        Conduta = null!;
        Orientacoes = null!;
        TextoOrigem = null!;
        HipotesesDiagnosticas = [];
        Avisos = [];
    }

    /// <summary>Registra o rascunho produzido pela IA para uma sessão de captura.</summary>
    public RascunhoIa(
        Guid sessaoCapturaId,
        Guid consultaId,
        string anamnese,
        string exameFisico,
        IEnumerable<string> hipoteses,
        string conduta,
        string orientacoes,
        string textoOrigem,
        string? modelo,
        bool parcial,
        IEnumerable<string> avisos,
        int duracaoMs)
    {
        Id = Guid.NewGuid();
        SessaoCapturaId = sessaoCapturaId;
        ConsultaId = consultaId;
        Anamnese = anamnese ?? string.Empty;
        ExameFisico = exameFisico ?? string.Empty;
        HipotesesDiagnosticas = [.. hipoteses ?? []];
        Conduta = conduta ?? string.Empty;
        Orientacoes = orientacoes ?? string.Empty;
        TextoOrigem = textoOrigem ?? string.Empty;
        Modelo = modelo;
        Parcial = parcial;
        Avisos = [.. avisos ?? []];
        GeradoEm = DateTime.UtcNow;
        DuracaoMs = duracaoMs < 0 ? 0 : duracaoMs;
    }

    /// <summary>
    /// Verdadeiro quando a IA não conseguiu extrair conteúdo clínico algum. Rascunho
    /// vazio não deve ser oferecido como se fosse prontuário: cai no caminho manual.
    /// </summary>
    public bool EstaVazio() =>
        string.IsNullOrWhiteSpace(Anamnese)
        && string.IsNullOrWhiteSpace(ExameFisico)
        && string.IsNullOrWhiteSpace(Conduta)
        && HipotesesDiagnosticas.Count == 0;
}
