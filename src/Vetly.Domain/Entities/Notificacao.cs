using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Uma notificação ao Responsável (RN-092/RN-093).
///
/// A notificação é <b>gravada antes de ser enviada</b>, e não gerada no momento do
/// disparo. Duas razões: o app precisa de uma caixa de entrada que sobrevive ao push
/// perdido — dispositivo desligado, token trocado, permissão negada — e o histórico
/// do que foi comunicado é o que permite responder "avisamos?" depois.
///
/// Push que falha não some: a notificação continua na caixa, e o Responsável a vê
/// quando abrir o app.
/// </summary>
public class Notificacao
{
    /// <summary>Tentativas de entrega por push antes de desistir do canal.</summary>
    public const int MaximoDeTentativas = 3;

    /// <summary>Identificador da notificação (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Responsável destinatário.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>Natureza da notificação — define ícone, agrupamento e prioridade no app.</summary>
    [Required]
    public TipoNotificacao Tipo { get; private set; }

    [Required]
    [MaxLength(120)]
    public string Titulo { get; private set; }

    [Required]
    [MaxLength(500)]
    public string Corpo { get; private set; }

    /// <summary>Situação da entrega.</summary>
    [Required]
    public StatusNotificacao Status { get; private set; }

    /// <summary>Animal a que a notificação se refere, quando há um.</summary>
    public Guid? AnimalId { get; private set; }

    /// <summary>Consulta relacionada, quando há uma.</summary>
    public Guid? ConsultaId { get; private set; }

    /// <summary>
    /// Para onde o app deve levar quem tocar na notificação. Guardado como rota
    /// interna, e não como URL: o destino é do app, não da API.
    /// </summary>
    [MaxLength(200)]
    public string? Destino { get; private set; }

    /// <summary>Quando deve ser enviada. Passado significa "assim que possível".</summary>
    public DateTime AgendadaPara { get; private set; }

    public DateTime? EnviadaEm { get; private set; }
    public DateTime? LidaEm { get; private set; }

    /// <summary>Tentativas de entrega por push já feitas.</summary>
    public int Tentativas { get; private set; }

    /// <summary>Por que a última tentativa falhou.</summary>
    [MaxLength(300)]
    public string? UltimoErro { get; private set; }

    public DateTime CriadaEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Notificacao()
    {
        Titulo = null!;
        Corpo = null!;
    }

    /// <summary>Cria uma notificação para o Responsável (RN-092).</summary>
    public Notificacao(
        Guid tutorId,
        TipoNotificacao tipo,
        string titulo,
        string corpo,
        DateTime? agendadaPara = null,
        Guid? animalId = null,
        Guid? consultaId = null,
        string? destino = null)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("O título da notificação é obrigatório.", nameof(titulo));

        if (string.IsNullOrWhiteSpace(corpo))
            throw new ArgumentException("O corpo da notificação é obrigatório.", nameof(corpo));

        Id = Guid.NewGuid();
        TutorId = tutorId;
        Tipo = tipo;
        Titulo = titulo.Trim();
        Corpo = corpo.Trim();
        AnimalId = animalId;
        ConsultaId = consultaId;
        Destino = destino;
        AgendadaPara = agendadaPara ?? DateTime.UtcNow;
        Status = StatusNotificacao.Pendente;
        CriadaEm = DateTime.UtcNow;
    }

    /// <summary>Verdadeiro quando já é hora de tentar entregar.</summary>
    public bool PodeEnviar(DateTime agora) =>
        Status == StatusNotificacao.Pendente && AgendadaPara <= agora;

    /// <summary>Registra a entrega bem-sucedida por push.</summary>
    public void RegistrarEnvio(DateTime quando)
    {
        Status = StatusNotificacao.Enviada;
        EnviadaEm = quando;
        Tentativas++;
    }

    /// <summary>
    /// Registra uma falha de entrega. Esgotadas as tentativas o canal desiste, mas a
    /// notificação <b>não</b> é descartada: ela permanece na caixa de entrada do app,
    /// porque push perdido não pode significar aviso perdido.
    /// </summary>
    public void RegistrarFalha(string erro)
    {
        Tentativas++;
        UltimoErro = erro.Length > 300 ? erro[..300] : erro;

        if (Tentativas >= MaximoDeTentativas)
            Status = StatusNotificacao.NaoEntregue;
    }

    /// <summary>
    /// Marca como lida quando o Responsável abre no app. A primeira leitura é a que
    /// fica — é o dado que diz se o aviso chegou a quem cuida do animal.
    /// </summary>
    public void MarcarComoLida(DateTime quando)
    {
        LidaEm ??= quando;
        Status = StatusNotificacao.Lida;
    }

    /// <summary>
    /// Verdadeiro quando a notificação chegou ao Responsável de alguma forma — por
    /// push entregue ou por leitura no app.
    /// </summary>
    public bool Alcancou() => Status is StatusNotificacao.Enviada or StatusNotificacao.Lida;
}
