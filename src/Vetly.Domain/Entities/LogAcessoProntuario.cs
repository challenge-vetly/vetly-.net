using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Registro imutável de um acesso ao prontuário de um animal (RN-086). Somente
/// inserção — visível ao Responsável no Centro de Privacidade.
/// </summary>
public class LogAcessoProntuario
{
    public Guid Id { get; private set; }

    [Required]
    public Guid AnimalId { get; private set; }

    [Required]
    public Guid VeterinarioId { get; private set; }

    [Required]
    public DateTime DataHora { get; private set; }

    [Required]
    [MaxLength(500)]
    public string Contexto { get; private set; }

    [Required]
    public BaseAcesso BaseAcesso { get; private set; }

    private LogAcessoProntuario()
    {
        Contexto = null!;
    }

    public LogAcessoProntuario(Guid animalId, Guid veterinarioId, DateTime dataHora, string contexto, BaseAcesso baseAcesso)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        VeterinarioId = veterinarioId;
        DataHora = dataHora;
        Contexto = contexto;
        BaseAcesso = baseAcesso;
    }
}
