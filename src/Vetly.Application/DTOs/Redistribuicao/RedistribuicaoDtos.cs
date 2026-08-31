using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Redistribuicao;

/// <summary>
/// Um veterinário candidato a assumir uma consulta redistribuída (RN-025).
///
/// A ordem dos candidatos não é por reputação: é pela proximidade do horário
/// original. Quem agendou às 14h de terça organizou o dia em torno disso, e trocar o
/// profissional já é uma quebra — trocar também o horário é outra.
/// </summary>
public class CandidatoARedistribuicaoDto
{
    public Guid VeterinarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Crmv { get; set; } = string.Empty;

    /// <summary>Clínica do candidato, quando ele atende vinculado.</summary>
    public Guid? EmpresaId { get; set; }

    /// <summary>Horário livre mais próximo do original.</summary>
    public Guid SlotId { get; set; }
    public DateTime NovoHorario { get; set; }

    /// <summary>Diferença em horas para o horário original — negativa quando é antes.</summary>
    public double DiferencaEmHoras { get; set; }

    /// <summary>Se atende a espécie do animal. Eliminatório: só entram candidatos que atendem.</summary>
    public bool AtendeEspecie { get; set; }

    public decimal NotaMedia { get; set; }

    /// <summary>Falso enquanto a nota não tem avaliações suficientes (RN-057).</summary>
    public bool NotaPublica { get; set; }
}

/// <summary>Escolha do novo responsável pela consulta (RN-025).</summary>
public class RedistribuirConsultaDto
{
    [Required(ErrorMessage = "O novo veterinário é obrigatório.")]
    public Guid NovoVeterinarioId { get; set; }

    [Required(ErrorMessage = "O novo horário é obrigatório.")]
    public Guid NovoSlotId { get; set; }

    /// <summary>
    /// Por que a consulta foi remanejada. Obrigatório: o Responsável recebe um aviso
    /// dizendo que o profissional mudou, e a mensagem sem motivo soa como erro do app.
    /// </summary>
    [Required(ErrorMessage = "O motivo da redistribuição é obrigatório.")]
    [MaxLength(300, ErrorMessage = "O motivo deve ter no máximo 300 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}

/// <summary>Resultado da redistribuição (RN-025).</summary>
public class RedistribuicaoRealizadaDto
{
    public Guid ConsultaId { get; set; }

    public Guid VeterinarioAnteriorId { get; set; }
    public Guid NovoVeterinarioId { get; set; }
    public string NovoVeterinarioNome { get; set; } = string.Empty;

    public DateTime HorarioAnterior { get; set; }
    public DateTime NovoHorario { get; set; }

    public string Motivo { get; set; } = string.Empty;

    /// <summary>
    /// Se o Responsável foi avisado. Redistribuir sem avisar seria trocar o
    /// profissional de alguém sem contar (RN-092).
    /// </summary>
    public bool ResponsavelNotificado { get; set; }

    public DateTime RealizadaEm { get; set; }
}
