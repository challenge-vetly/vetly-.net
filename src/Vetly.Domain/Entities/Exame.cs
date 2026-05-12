namespace Vetly.Domain.Entities;

public class Exame
{
    public Guid Id { get; private set; }
    public Guid AnimalId { get; private set; }
    public Guid VeterinarioId { get; private set; }
    public string TipoSolicitacao { get; private set; }
    public string? Resultado { get; private set; }
    public bool LiberadoAoTutor { get; private set; }
    public DateTime DataSolicitacao { get; private set; }
    public DateTime? DataResultado { get; private set; }

    private Exame() { }

    public Exame(Guid animalId, Guid veterinarioId, string tipoSolicitacao)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        VeterinarioId = veterinarioId;
        TipoSolicitacao = tipoSolicitacao;
        DataSolicitacao = DateTime.UtcNow;
    }

    public void RegistrarResultado(string resultado)
    {
        Resultado = resultado;
        DataResultado = DateTime.UtcNow;
    }

    public void LiberarAoTutor()
    {
        if (string.IsNullOrWhiteSpace(Resultado))
            throw new InvalidOperationException("Não é possível liberar exame sem resultado.");

        LiberadoAoTutor = true;
    }
}
