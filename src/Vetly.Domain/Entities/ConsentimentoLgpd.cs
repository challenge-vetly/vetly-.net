using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Registro de consentimento LGPD granular de um responsável para uma finalidade
/// específica (RN-041/042/043, RN-084). Cada concessão gera um registro novo;
/// a revogação nunca apaga um registro existente, apenas grava a data de revogação —
/// o histórico completo permanece consultável (RN-044, RN-086).
/// </summary>
public class ConsentimentoLgpd
{
    public Guid Id { get; private set; }

    /// <summary>Id do responsável dono deste consentimento. Chave estrangeira para TB_RESPONSAVEL.</summary>
    [Required]
    public Guid ResponsavelId { get; private set; }

    [Required]
    public FinalidadeConsentimento Finalidade { get; private set; }

    /// <summary>
    /// Sempre true nesta versão — todo registro criado por <see cref="ConsentimentoLgpd"/>
    /// representa uma concessão. Mantido como campo explícito para espelhar o modelo de
    /// dados da spec v2 e permitir extensão futura sem quebrar o schema.
    /// </summary>
    public bool Concedido { get; private set; }

    [Required]
    public DateTime DataConcessao { get; private set; }

    /// <summary>Nulo enquanto o consentimento estiver ativo. Preenchido na revogação (RN-044).</summary>
    public DateTime? DataRevogacao { get; private set; }

    /// <summary>Um consentimento está ativo enquanto não tiver sido revogado.</summary>
    public bool Ativo => DataRevogacao is null;

    private ConsentimentoLgpd() { }

    public ConsentimentoLgpd(Guid responsavelId, FinalidadeConsentimento finalidade, DateTime agora)
    {
        Id = Guid.NewGuid();
        ResponsavelId = responsavelId;
        Finalidade = finalidade;
        Concedido = true;
        DataConcessao = agora;
    }

    /// <summary>Revoga este consentimento a partir de <paramref name="agora"/>. Idempotente.</summary>
    public void Revogar(DateTime agora)
    {
        DataRevogacao ??= agora;
    }
}
