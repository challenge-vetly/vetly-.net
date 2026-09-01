using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Animal;

/// <summary>Peso aferido no atendimento (RN-081).</summary>
public class RegistrarPesoDto
{
    /// <summary>
    /// Peso em quilos. Deve ser maior que zero: dose se calcula sobre ele, e zero
    /// produziria posologia sem sentido.
    /// </summary>
    [Range(0.01, 999.99, ErrorMessage = "O peso deve estar entre 0,01 kg e 999,99 kg.")]
    public decimal PesoKg { get; set; }
}
