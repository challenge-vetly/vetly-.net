using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa uma consulta veterinária agendada ou realizada na plataforma Vetly.
/// O agendamento só é confirmado após pagamento processado (RN-015).
/// </summary>
public class Consulta
{
    /// <summary>Identificador único da consulta (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Data e hora agendada para a consulta. Indexada no banco para buscas por período.</summary>
    [Required]
    public DateTime DataHora { get; private set; }

    /// <summary>Modalidade da consulta: presencial ou remota (teleconsulta).</summary>
    [Required]
    public ModalidadeAtendimento Modalidade { get; private set; }

    /// <summary>Id do veterinário responsável pela consulta. Chave estrangeira para TB_VETERINARIO.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Id do animal atendido. Chave estrangeira para TB_ANIMAL.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Id do tutor responsável. Chave estrangeira para TB_TUTOR.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>
    /// Indica se o veterinário validou o diagnóstico sugerido pela IA.
    /// RN-024: documentos só podem ser gerados após validação manual.
    /// </summary>
    public bool DiagnosticoValidado { get; private set; }

    /// <summary>Indica se o veterinário validou o protocolo de tratamento sugerido pela IA.</summary>
    public bool ProtocoloValidado { get; private set; }

    /// <summary>Status do pagamento associado a esta consulta.</summary>
    [Required]
    public StatusPagamento StatusPagamento { get; private set; }

    /// <summary>Indica se a consulta foi cancelada.</summary>
    public bool Cancelada { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Consulta() { }

    /// <summary>
    /// Cria uma nova consulta com status de pagamento pendente.
    /// O agendamento só deve ser confirmado após chamar <see cref="ConfirmarPagamento"/> (RN-015).
    /// </summary>
    public Consulta(DateTime dataHora, ModalidadeAtendimento modalidade, Guid veterinarioId, Guid animalId, Guid tutorId)
    {
        Id = Guid.NewGuid();
        DataHora = dataHora;
        Modalidade = modalidade;
        VeterinarioId = veterinarioId;
        AnimalId = animalId;
        TutorId = tutorId;
        StatusPagamento = StatusPagamento.Pendente; // pagamento confirmado é pré-requisito (RN-015)
    }

    /// <summary>Marca o pagamento como confirmado, liberando o agendamento (RN-015).</summary>
    public void ConfirmarPagamento() => StatusPagamento = StatusPagamento.Confirmado;

    /// <summary>Registra a validação manual do diagnóstico pelo veterinário (RN-024).</summary>
    public void ValidarDiagnostico() => DiagnosticoValidado = true;

    /// <summary>Registra a validação manual do protocolo de tratamento pelo veterinário.</summary>
    public void ValidarProtocolo() => ProtocoloValidado = true;

    /// <summary>Cancela a consulta. O reembolso é calculado pelo Strategy de cancelamento (RN-019/020/021).</summary>
    public void Cancelar() => Cancelada = true;

    /// <summary>Reagenda a consulta para uma nova data e hora.</summary>
    public void Reagendar(DateTime novaDataHora) => DataHora = novaDataHora;

    /// <summary>
    /// Verifica se a consulta está apta para geração de documentos.
    /// Requer diagnóstico validado E pagamento confirmado.
    /// </summary>
    public bool PodeGerarDocumentos() =>
        DiagnosticoValidado && StatusPagamento == StatusPagamento.Confirmado;
}
