namespace Vetly.Domain.Entities;

public class Internacao
{
    public Guid Id { get; private set; }
    public Guid AnimalId { get; private set; }
    public Guid VeterinarioId { get; private set; }
    public decimal ValorCaucao { get; private set; }
    public decimal ValorTotalApurado { get; private set; }
    public DateTime DataAbertura { get; private set; }
    public DateTime? DataAlta { get; private set; }
    public string ProcedimentosDiarios { get; private set; }

    private Internacao()
    {
        ProcedimentosDiarios = null!;
    }

    public Internacao(Guid animalId, Guid veterinarioId, decimal valorCaucao)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        VeterinarioId = veterinarioId;
        ValorCaucao = valorCaucao;
        DataAbertura = DateTime.UtcNow;
        ProcedimentosDiarios = "[]";
    }

    public void RegistrarProcedimentoDiario(string procedimentoJson)
    {
        ProcedimentosDiarios = procedimentoJson;
    }

    public void ApurarValor(decimal valor)
    {
        ValorTotalApurado += valor;
    }

    public void DarAlta()
    {
        if (DataAlta.HasValue)
            throw new InvalidOperationException("Internação já encerrada.");

        DataAlta = DateTime.UtcNow;
    }

    public bool EstaAtiva() => !DataAlta.HasValue;

    public int DiasInternado() =>
        (int)((DataAlta ?? DateTime.UtcNow) - DataAbertura).TotalDays + 1;
}
