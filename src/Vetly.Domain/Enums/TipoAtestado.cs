namespace Vetly.Domain.Enums;

/// <summary>
/// Subtipo do atestado veterinário (RN-086).
///
/// Não é rótulo: cada subtipo declara uma coisa diferente, e o corpo do documento
/// muda com ele. Emitir os três com o mesmo texto seria emitir um documento que não
/// diz o que precisa dizer.
/// </summary>
public enum TipoAtestado
{
    /// <summary>Registra o óbito do animal, constatado no atendimento.</summary>
    Obito = 1,

    /// <summary>Atesta a condição clínica apurada no exame. É o caso comum.</summary>
    Saude = 2,

    /// <summary>Comprova as vacinas aplicadas e a carteira em dia.</summary>
    Vacinacao = 3
}
