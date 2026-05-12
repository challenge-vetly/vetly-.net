using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

public class Consulta
{
    public Guid Id { get; private set; }
    public DateTime DataHora { get; private set; }
    public ModalidadeAtendimento Modalidade { get; private set; }
    public Guid VeterinarioId { get; private set; }
    public Guid AnimalId { get; private set; }
    public Guid TutorId { get; private set; }
    public bool DiagnosticoValidado { get; private set; }
    public bool ProtocoloValidado { get; private set; }
    public StatusPagamento StatusPagamento { get; private set; }
    public bool Cancelada { get; private set; }

    private Consulta() { }

    public Consulta(DateTime dataHora, ModalidadeAtendimento modalidade, Guid veterinarioId, Guid animalId, Guid tutorId)
    {
        Id = Guid.NewGuid();
        DataHora = dataHora;
        Modalidade = modalidade;
        VeterinarioId = veterinarioId;
        AnimalId = animalId;
        TutorId = tutorId;
        StatusPagamento = StatusPagamento.Pendente;
    }

    public void ConfirmarPagamento() => StatusPagamento = StatusPagamento.Confirmado;

    public void ValidarDiagnostico() => DiagnosticoValidado = true;

    public void ValidarProtocolo() => ProtocoloValidado = true;

    public void Cancelar() => Cancelada = true;

    public void Reagendar(DateTime novaDataHora) => DataHora = novaDataHora;

    public bool PodeGerarDocumentos() => DiagnosticoValidado && StatusPagamento == StatusPagamento.Confirmado;
}
