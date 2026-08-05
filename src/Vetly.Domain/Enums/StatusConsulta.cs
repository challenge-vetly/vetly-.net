namespace Vetly.Domain.Enums;

/// <summary>Máquina de estados do slot/consulta (RN-058/RN-061).</summary>
public enum StatusConsulta
{
    EmCheckout = 1,
    Confirmada = 2,
    Realizada = 3,
    Cancelada = 4,
    NoShowResponsavel = 5,
    NoShowVeterinario = 6
}
