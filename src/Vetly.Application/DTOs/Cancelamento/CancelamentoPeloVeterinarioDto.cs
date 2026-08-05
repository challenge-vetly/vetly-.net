namespace Vetly.Application.DTOs.Cancelamento;

/// <summary>Resultado do cancelamento (ou no-show) pelo veterinário — crédito de cortesia + strike (RN-065/066/067).</summary>
public class CancelamentoPeloVeterinarioDto
{
    /// <summary>Crédito de cortesia lançado no saldo Vetly do responsável (10% do valor, teto R$ 30).</summary>
    public decimal CreditoCortesia { get; set; }

    public bool StrikeRegistrado { get; set; }

    /// <summary>True se este strike atingiu o limiar (3 em 90 dias) e suspendeu o perfil por 7 dias.</summary>
    public bool VeterinarioSuspenso { get; set; }
}
