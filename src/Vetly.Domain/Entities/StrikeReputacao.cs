namespace Vetly.Domain.Entities;

/// <summary>
/// Registro de um strike de reputação de um veterinário (cancelamento ou no-show pelo
/// vet — RN-065/066/067). Owned por <see cref="Veterinario"/>, sem identidade própria.
/// </summary>
public class StrikeReputacao
{
    public DateTime Data { get; private set; }
    public string Motivo { get; private set; }

    private StrikeReputacao()
    {
        Motivo = null!;
    }

    public StrikeReputacao(DateTime data, string motivo)
    {
        Data = data;
        Motivo = motivo;
    }
}
