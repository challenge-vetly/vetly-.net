using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Captura;

/// <summary>
/// Decisão do veterinário sobre o rascunho da IA (RN-082).
///
/// Três caminhos, e a escolha é explícita: não há aprovação por omissão. Aprovar sem
/// ler e corrigir antes de aprovar não podem ficar registrados da mesma forma.
/// </summary>
public class DecisaoDoProntuarioDto
{
    /// <summary><c>Aprovado</c>, <c>Corrigido</c> ou <c>NaoAprovado</c>.</summary>
    [Required(ErrorMessage = "A decisão é obrigatória.")]
    public DecisaoSobreRascunho Decisao { get; set; }

    /// <summary>
    /// Conteúdo corrigido. Obrigatório em <c>Corrigido</c> — corrigir sem dizer o que
    /// mudou não é corrigir.
    /// </summary>
    public ConteudoDoProntuarioDto? Correcao { get; set; }

    /// <summary>
    /// Motivo. Obrigatório em <c>NaoAprovado</c>: recusar sem registrar por quê deixa
    /// a trilha de auditoria sem a informação que mais importa.
    /// </summary>
    [MaxLength(2000, ErrorMessage = "A justificativa deve ter no máximo 2000 caracteres.")]
    public string? Justificativa { get; set; }
}

/// <summary>Conteúdo clínico de um prontuário.</summary>
public class ConteudoDoProntuarioDto
{
    [MaxLength(20000)]
    public string Anamnese { get; set; } = string.Empty;

    [MaxLength(20000)]
    public string ExameFisico { get; set; } = string.Empty;

    public List<string> HipotesesDiagnosticas { get; set; } = [];

    [MaxLength(20000)]
    public string Conduta { get; set; } = string.Empty;

    [MaxLength(20000)]
    public string Orientacoes { get; set; } = string.Empty;
}

/// <summary>
/// Prontuário escrito à mão pelo veterinário (RN-085).
///
/// É o caminho quando não houve captura — plano Básico, falha da transcrição ou
/// recusa do rascunho. O atendimento aconteceu e precisa virar prontuário de algum
/// jeito.
/// </summary>
public class ProntuarioManualDto
{
    [Required(ErrorMessage = "O conteúdo do prontuário é obrigatório.")]
    public ConteudoDoProntuarioDto Conteudo { get; set; } = new();
}

/// <summary>Resultado da decisão, com o estado em que o ciclo ficou (§7.3).</summary>
public class DecisaoRegistradaDto
{
    public Guid ConsultaId { get; set; }

    /// <summary>Registro de auditoria gravado — append-only, não se altera depois.</summary>
    public Guid LogAuditoriaId { get; set; }

    public DecisaoSobreRascunho Decisao { get; set; }

    /// <summary>
    /// Falso quando o veterinário não aprovou: sem validação não se gera documento
    /// (RN-082).
    /// </summary>
    public bool DiagnosticoValidado { get; set; }

    /// <summary>Estado do ciclo de documentação (§7.3).</summary>
    public EstadoSessaoCaptura? EstadoDaSessao { get; set; }

    public DateTime RegistradoEm { get; set; }
}

/// <summary>Uma decisão registrada na trilha de auditoria da IA (RN-082).</summary>
public class LogAuditoriaIaDto
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }
    public Guid? RascunhoIaId { get; set; }
    public Guid? VeterinarioId { get; set; }
    public DecisaoSobreRascunho Decisao { get; set; }
    public string ConteudoFinal { get; set; } = string.Empty;
    public string? Justificativa { get; set; }

    /// <summary>O veterinário alterou o que a IA sugeriu.</summary>
    public bool AlterouSugestao { get; set; }

    public string? Modelo { get; set; }
    public DateTime RegistradoEm { get; set; }
}
