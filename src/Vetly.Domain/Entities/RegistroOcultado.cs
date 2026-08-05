using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Marca um prontuário como ocultado da visão de veterinários que não o produziram
/// (RN-088). O Responsável sempre vê tudo — a ocultação só afeta a visão do vet.
/// Nunca é criado para um prontuário classificado como alerta de segurança
/// (<see cref="Prontuario.AlertaSeguranca"/>) — essa invariante é garantida por
/// <see cref="Animal.OcultarRegistro"/>, que é o único caminho de criação legítimo.
/// </summary>
public class RegistroOcultado
{
    public Guid Id { get; private set; }

    /// <summary>Id do animal dono do registro ocultado. Chave estrangeira para TB_ANIMAL.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Id do prontuário ocultado. Chave estrangeira para TB_PRONTUARIO.</summary>
    [Required]
    public Guid ProntuarioId { get; private set; }

    [Required]
    public DateTime DataOcultacao { get; private set; }

    private RegistroOcultado() { }

    public RegistroOcultado(Guid animalId, Guid prontuarioId, DateTime agora)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        ProntuarioId = prontuarioId;
        DataOcultacao = agora;
    }
}
