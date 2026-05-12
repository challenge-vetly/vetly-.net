using System.Text.RegularExpressions;

namespace Vetly.Domain.ValueObjects;

public sealed class Crmv
{
    private static readonly Regex Formato = new(@"^\d{4,6}-[A-Z]{2}$", RegexOptions.Compiled);

    public string Valor { get; }

    public Crmv(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException("CRMV não pode ser vazio.");

        if (!Formato.IsMatch(valor))
            throw new ArgumentException($"CRMV '{valor}' está em formato inválido. Use o padrão XXXXXX-UF.");

        Valor = valor.ToUpperInvariant();
    }

    public override string ToString() => Valor;
    public override bool Equals(object? obj) => obj is Crmv other && Valor == other.Valor;
    public override int GetHashCode() => Valor.GetHashCode();

    public static implicit operator string(Crmv crmv) => crmv.Valor;
}
