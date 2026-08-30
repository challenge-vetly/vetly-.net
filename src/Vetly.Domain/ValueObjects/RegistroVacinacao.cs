namespace Vetly.Domain.ValueObjects;

/// <summary>
/// Um registro da carteira de vacinação do animal: qual vacina foi aplicada e quando.
/// A carteira alimenta o calendário de obrigações do pet (RN-046) e o briefing pré-consulta.
/// Persistida como JSON em coluna CLOB — ver <c>AnimalConfiguration</c>.
/// </summary>
public sealed class RegistroVacinacao
{
    /// <summary>Tipo/nome da vacina aplicada (ex: "V10", "Antirrábica").</summary>
    public string Tipo { get; init; } = string.Empty;

    /// <summary>Data em que a vacina foi aplicada (UTC).</summary>
    public DateTime AplicadaEm { get; init; }

    /// <summary>Construtor sem parâmetros exigido pela desserialização JSON.</summary>
    public RegistroVacinacao() { }

    /// <summary>Cria um registro de vacinação.</summary>
    public RegistroVacinacao(string tipo, DateTime aplicadaEm)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            throw new ArgumentException("O tipo da vacina é obrigatório.", nameof(tipo));

        Tipo = tipo;
        AplicadaEm = aplicadaEm;
    }
}
