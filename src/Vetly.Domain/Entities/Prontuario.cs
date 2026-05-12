namespace Vetly.Domain.Entities;

public class Prontuario
{
    public Guid Id { get; private set; }
    public Guid ConsultaId { get; private set; }
    public Guid AnimalId { get; private set; }
    public string DadosClinicos { get; private set; }
    public Guid? VersaoOriginalId { get; private set; }
    public DateTime? DataCorrecao { get; private set; }
    public string? JustificativaCorrecao { get; private set; }
    public string? CrmvSolicitanteCorrecao { get; private set; }
    public DateTime DataCriacao { get; private set; }

    private Prontuario()
    {
        DadosClinicos = null!;
    }

    public Prontuario(Guid consultaId, Guid animalId, string dadosClinicos)
    {
        Id = Guid.NewGuid();
        ConsultaId = consultaId;
        AnimalId = animalId;
        DadosClinicos = dadosClinicos;
        DataCriacao = DateTime.UtcNow;
    }

    public Prontuario CriarCorrecao(string novosDadosClinicos, string? justificativa, string crmvSolicitante)
    {
        var correcao = new Prontuario(ConsultaId, AnimalId, novosDadosClinicos)
        {
            VersaoOriginalId = Id,
            DataCorrecao = DateTime.UtcNow,
            JustificativaCorrecao = justificativa,
            CrmvSolicitanteCorrecao = crmvSolicitante
        };
        return correcao;
    }

    public bool ExigeJustificativa() =>
        DataCriacao < DateTime.UtcNow.AddHours(-24);
}
