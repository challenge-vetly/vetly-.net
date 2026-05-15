using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um lembrete agendado para um tutor (RN-029/030).
/// Controla tentativas de contato e alerta a clinica apos 3 tentativas sem resposta.
/// </summary>
public class LembreteAgendado
{
    public Guid Id { get; private set; }

    [Required]
    public Guid AnimalId { get; private set; }

    [Required]
    public Guid TutorId { get; private set; }

    [Required]
    public TipoLembrete Tipo { get; private set; }

    /// <summary>Data do evento que originou o lembrete (ex: data de vacina vencida).</summary>
    [Required]
    public DateTime DataEvento { get; private set; }

    /// <summary>Quantidade de tentativas de contato realizadas com o tutor.</summary>
    public int TentativasRealizadas { get; private set; }

    /// <summary>Indica se o tutor respondeu ao lembrete, encerrando a regua.</summary>
    public bool TutorRespondeu { get; private set; }

    /// <summary>Indica se o alerta foi enviado a clinica (apos 3 tentativas sem resposta).</summary>
    public bool AlertaEnviadoClinica { get; private set; }

    private LembreteAgendado() { }

    public LembreteAgendado(Guid animalId, Guid tutorId, TipoLembrete tipo, DateTime dataEvento)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        TutorId = tutorId;
        Tipo = tipo;
        DataEvento = dataEvento;
    }

    /// <summary>
    /// Registra uma tentativa de contato.
    /// Aciona alerta para a clinica apos 3 tentativas sem resposta do tutor (RN-030).
    /// </summary>
    public void RegistrarTentativa()
    {
        TentativasRealizadas++;
        if (TentativasRealizadas >= 3 && !TutorRespondeu)
            AlertaEnviadoClinica = true;
    }

    /// <summary>Registra a resposta do tutor, encerrando a regua de lembretes (RN-029).</summary>
    public void RegistrarResposta() => TutorRespondeu = true;
}
