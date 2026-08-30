namespace Vetly.Domain.Enums;

/// <summary>
/// Sexo do animal, informado no cadastro do pet.
/// Compõe o perfil clínico usado no briefing e no contexto entregue à IA (RN-078).
/// </summary>
public enum SexoAnimal
{
    /// <summary>Animal do sexo masculino.</summary>
    Macho = 1,

    /// <summary>Animal do sexo feminino.</summary>
    Femea = 2
}
