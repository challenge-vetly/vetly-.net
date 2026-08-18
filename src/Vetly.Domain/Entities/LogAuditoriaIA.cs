using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.Domain.Entities;

/// <summary>
/// Trilha de auditoria de uma sugestão de IA (RN-098). Nasce com a sugestão da IA
/// (pendente de decisão) e é finalizada exatamente uma vez, quando o veterinário decide
/// (Aprovar/Não aprovar/Corrigir) — depois disso, imutável: nenhum outro método altera o
/// registro. Retido junto ao prontuário para defesa jurídica e melhoria do modelo.
/// </summary>
public class LogAuditoriaIA
{
    public Guid Id { get; private set; }

    [Required]
    public Guid ConsultaId { get; private set; }

    [Required]
    public Guid VeterinarioId { get; private set; }

    [Required]
    [MaxLength(15)]
    public string Crmv { get; private set; }

    [Required]
    public DateTime Timestamp { get; private set; }

    [Required]
    [MaxLength(100)]
    public string VersaoModelo { get; private set; }

    [Required]
    public TipoSugestaoIA TipoSugestao { get; private set; }

    /// <summary>Conteúdo sugerido pela IA no momento da geração.</summary>
    [Required]
    public string ConteudoSugerido { get; private set; }

    /// <summary>
    /// Decisão do veterinário. Nulo enquanto pendente — só é preenchida por
    /// <see cref="RegistrarDecisao"/>, chamado no máximo uma vez.
    /// </summary>
    public DecisaoVeterinario? Decisao { get; private set; }

    /// <summary>
    /// Conteúdo final autoritativo: igual ao sugerido se Aprovar, reescrito pelo vet se
    /// Corrigir, nulo se Não aprovar (ciclo encerrado sem conteúdo final — RN-099).
    /// </summary>
    public string? ConteudoFinal { get; private set; }

    /// <summary>
    /// True enquanto nada foi finalizado ainda. Checa os dois campos (não só Decisao)
    /// porque <see cref="RegistrarArtefatoAutomatico"/> cria logs já com ConteudoFinal
    /// preenchido mas sem uma decisão de vet propriamente dita (não há Aprovar/Corrigir
    /// para artefatos que a IA gera sozinha — RN-097).
    /// </summary>
    public bool Pendente => Decisao is null && ConteudoFinal is null;

    private LogAuditoriaIA()
    {
        Crmv = null!;
        VersaoModelo = null!;
        ConteudoSugerido = null!;
    }

    /// <summary>Cria um log a partir de uma sugestão da IA, pendente de decisão do veterinário.</summary>
    public LogAuditoriaIA(
        Guid consultaId, Guid veterinarioId, string crmv, string versaoModelo,
        TipoSugestaoIA tipoSugestao, string conteudoSugerido, DateTime timestamp)
    {
        Id = Guid.NewGuid();
        ConsultaId = consultaId;
        VeterinarioId = veterinarioId;
        Crmv = crmv;
        VersaoModelo = versaoModelo;
        TipoSugestao = tipoSugestao;
        ConteudoSugerido = conteudoSugerido;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Cria um log já completo para artefatos que a IA gera sozinha, sem decisão do vet
    /// (ex: formatação de documentos a partir do estado final já decidido — RN-099.1).
    /// </summary>
    public static LogAuditoriaIA RegistrarArtefatoAutomatico(
        Guid consultaId, Guid veterinarioId, string crmv, string versaoModelo,
        TipoSugestaoIA tipoSugestao, string conteudo, DateTime timestamp)
    {
        var log = new LogAuditoriaIA(consultaId, veterinarioId, crmv, versaoModelo, tipoSugestao, conteudo, timestamp)
        {
            ConteudoFinal = conteudo
        };
        return log;
    }

    /// <summary>
    /// Registra a decisão do veterinário, finalizando o log (RN-099). Só pode ser chamado
    /// uma vez — chamadas subsequentes indicam um bug no fluxo de decisão, não um cenário
    /// de negócio válido.
    /// </summary>
    public void RegistrarDecisao(DecisaoVeterinario decisao, string? conteudoFinal)
    {
        if (Decisao is not null)
            throw new DomainException("IA-002", "Este log de auditoria de IA já foi finalizado.");

        Decisao = decisao;
        ConteudoFinal = conteudoFinal;
    }
}
