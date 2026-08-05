using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa uma consulta veterinária agendada ou realizada na plataforma Vetly.
/// O ciclo de vida é uma máquina de estados explícita (RN-058/RN-061): o agendamento
/// nasce em EmCheckout, com um lock de 10 min, e transiciona para Confirmada, Realizada,
/// Cancelada, NoShowResponsavel ou NoShowVeterinario — nunca retrocede.
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

    /// <summary>Tipo de serviço agendado. Procedimentos físicos exigem modalidade presencial (RN-057).</summary>
    [Required]
    public TipoServico TipoServico { get; private set; }

    /// <summary>Id do veterinário responsável pela consulta. Chave estrangeira para TB_VETERINARIO.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Id do animal atendido. Chave estrangeira para TB_ANIMAL.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Id do responsavel responsável. Chave estrangeira para TB_RESPONSAVEL.</summary>
    [Required]
    public Guid ResponsavelId { get; private set; }

    /// <summary>
    /// Pré-sintomas relatados no agendamento (texto guiado + mídia — RN-059).
    /// Alimenta o briefing pré-consulta e a IA da consulta (RN-096).
    /// </summary>
    [MaxLength(4000)]
    public string? PreSintomas { get; private set; }

    /// <summary>Estado atual do slot/consulta (RN-058/RN-061).</summary>
    [Required]
    public StatusConsulta Status { get; private set; }

    /// <summary>Momento em que o lock de checkout expira. Preenchido ao entrar em EmCheckout (RN-058).</summary>
    public DateTime? LockCheckoutExpiraEm { get; private set; }

    /// <summary>Quantidade de vezes que a consulta foi remarcada.</summary>
    public int ContadorRemarcacoes { get; private set; }

    /// <summary>Momento em que o veterinário marcou a consulta como realizada. Abre a janela de avaliação (RN-076).</summary>
    public DateTime? DataRealizada { get; private set; }

    /// <summary>
    /// Indica se o veterinário validou o diagnóstico sugerido pela IA.
    /// RN-024: documentos só podem ser gerados após validação manual.
    /// </summary>
    public bool DiagnosticoValidado { get; private set; }

    /// <summary>Indica se o veterinário validou o protocolo de tratamento sugerido pela IA.</summary>
    public bool ProtocoloValidado { get; private set; }

    /// <summary>
    /// Diagnóstico final autoritativo (aprovado ou reescrito pelo vet — RN-099). Gate
    /// principal de <see cref="EstadoFinalDefinido"/>.
    /// </summary>
    public string? DiagnosticoFinal { get; private set; }

    /// <summary>Protocolo final autoritativo (aprovado ou reescrito pelo vet — RN-099).</summary>
    public string? ProtocoloFinal { get; private set; }

    /// <summary>
    /// True assim que o diagnóstico final é definido (RN-099). Gate de geração de
    /// documentos clínicos — evolução da RN-024 (CONSULTA-012 se ainda false).
    /// </summary>
    public bool EstadoFinalDefinido { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Consulta() { }

    /// <summary>
    /// Cria uma nova consulta já em estado EmCheckout (RN-058). O lock de 10 min só é
    /// definido ao chamar <see cref="IniciarCheckout"/> — normalmente logo em seguida,
    /// dentro do mesmo fluxo de agendamento.
    /// </summary>
    public Consulta(
        DateTime dataHora, ModalidadeAtendimento modalidade, TipoServico tipoServico,
        Guid veterinarioId, Guid animalId, Guid responsavelId, string? preSintomas = null)
    {
        Id = Guid.NewGuid();
        DataHora = dataHora;
        Modalidade = modalidade;
        TipoServico = tipoServico;
        VeterinarioId = veterinarioId;
        AnimalId = animalId;
        ResponsavelId = responsavelId;
        PreSintomas = preSintomas;
        Status = StatusConsulta.EmCheckout;
    }

    /// <summary>Inicia (ou reinicia) o lock de checkout de 10 minutos a partir de <paramref name="agora"/> (RN-058).</summary>
    public void IniciarCheckout(DateTime agora) => LockCheckoutExpiraEm = agora.AddMinutes(10);

    /// <summary>
    /// Confirma o pagamento e transiciona EmCheckout → Confirmada (RN-058).
    /// Exige que o lock de checkout ainda não tenha expirado.
    /// </summary>
    public void ConfirmarPagamento(DateTime agora)
    {
        if (Status != StatusConsulta.EmCheckout)
            throw new DomainException("CONSULTA-010",
                $"Não é possível confirmar pagamento a partir do estado '{Status}'.");

        if (LockCheckoutExpiraEm is null || agora > LockCheckoutExpiraEm.Value)
            throw new DomainException("CONSULTA-011",
                "O lock de checkout expirou. Reinicie o agendamento.");

        Status = StatusConsulta.Confirmada;
    }

    /// <summary>Registra a validação manual do diagnóstico pelo veterinário (RN-024).</summary>
    public void ValidarDiagnostico() => DiagnosticoValidado = true;

    /// <summary>Registra a validação manual do protocolo de tratamento pelo veterinário.</summary>
    public void ValidarProtocolo() => ProtocoloValidado = true;

    /// <summary>
    /// Cancela a consulta (RN-058/061). Só é possível a partir de EmCheckout ou Confirmada —
    /// o reembolso é calculado pelo Strategy de cancelamento (RN-019/020/021).
    /// </summary>
    public void Cancelar()
    {
        if (Status is not (StatusConsulta.EmCheckout or StatusConsulta.Confirmada))
            throw new DomainException("CONSULTA-010",
                $"Não é possível cancelar a partir do estado '{Status}'.");

        Status = StatusConsulta.Cancelada;
    }

    /// <summary>
    /// Marca a consulta como realizada (RN-061). Só é possível a partir de Confirmada.
    /// A checagem de que quem chama é o veterinário responsável é feita pelo Application
    /// (via ICurrentUserService), não aqui — este método só valida a transição de estado.
    /// </summary>
    public void MarcarRealizada(DateTime agora)
    {
        if (Status != StatusConsulta.Confirmada)
            throw new DomainException("CONSULTA-010",
                $"Não é possível marcar como realizada a partir do estado '{Status}'.");

        Status = StatusConsulta.Realizada;
        DataRealizada = agora;
    }

    /// <summary>Registra no-show do responsável (RN-064). Só é possível a partir de Confirmada.</summary>
    public void RegistrarNoShowResponsavel()
    {
        if (Status != StatusConsulta.Confirmada)
            throw new DomainException("CONSULTA-010",
                $"Não é possível registrar no-show a partir do estado '{Status}'.");

        Status = StatusConsulta.NoShowResponsavel;
    }

    /// <summary>Registra no-show do veterinário (RN-066). Só é possível a partir de Confirmada.</summary>
    public void RegistrarNoShowVeterinario()
    {
        if (Status != StatusConsulta.Confirmada)
            throw new DomainException("CONSULTA-010",
                $"Não é possível registrar no-show a partir do estado '{Status}'.");

        Status = StatusConsulta.NoShowVeterinario;
    }

    /// <summary>
    /// Reagenda a consulta para uma nova data e hora, incrementando o contador de
    /// remarcações (RN-022). Não é permitido em estados finais.
    /// </summary>
    public void Reagendar(DateTime novaDataHora)
    {
        if (Status is StatusConsulta.Realizada or StatusConsulta.Cancelada
            or StatusConsulta.NoShowResponsavel or StatusConsulta.NoShowVeterinario)
            throw new DomainException("CONSULTA-010",
                $"Não é possível remarcar a partir do estado '{Status}'.");

        DataHora = novaDataHora;
        ContadorRemarcacoes++;
    }

    /// <summary>Define o diagnóstico final autoritativo (RN-099) e abre o gate de documentos (RN-024).</summary>
    public void DefinirDiagnosticoFinal(string diagnosticoFinal)
    {
        DiagnosticoFinal = diagnosticoFinal;
        EstadoFinalDefinido = true;
    }

    /// <summary>Define o protocolo final autoritativo (RN-099).</summary>
    public void DefinirProtocoloFinal(string protocoloFinal) => ProtocoloFinal = protocoloFinal;

    /// <summary>
    /// Verifica se a consulta está apta para geração de documentos.
    /// Requer estado final definido (RN-099/024) E pagamento confirmado (Confirmada ou já Realizada).
    /// </summary>
    public bool PodeGerarDocumentos() =>
        EstadoFinalDefinido && Status is StatusConsulta.Confirmada or StatusConsulta.Realizada;
}
