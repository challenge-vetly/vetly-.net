using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Autorização do Responsável para que um veterinário fora do seu histórico acesse os
/// dados clínicos do animal — a colmeia (RN-090/RN-105).
///
/// O ponto é de quem parte: <b>quem concede é o Responsável</b>, não a clínica. O
/// histórico do animal é dele, e o veterinário novo só alcança o que foi autorizado,
/// pelo tempo autorizado. Sem isso, "compartilhar o histórico" viraria "qualquer
/// profissional cadastrado lê o prontuário de qualquer animal".
///
/// A concessão nasce com prazo. Acesso clínico que não expira sozinho é acesso que
/// ninguém lembra de revogar.
/// </summary>
public class AcessoColmeia
{
    /// <summary>Validade padrão da concessão, quando o Responsável não escolhe outra.</summary>
    public static readonly TimeSpan ValidadePadrao = TimeSpan.FromDays(30);

    /// <summary>Validade máxima que uma concessão pode ter.</summary>
    public static readonly TimeSpan ValidadeMaxima = TimeSpan.FromDays(365);

    /// <summary>Identificador da concessão (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Animal cujo histórico foi compartilhado.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Responsável que concedeu. É dele o histórico.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>Veterinário autorizado.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Clínica do veterinário, quando ele atende vinculado.</summary>
    public Guid? EmpresaId { get; private set; }

    /// <summary>Até onde a autorização vai (RN-090).</summary>
    [Required]
    public EscopoAcessoColmeia Escopo { get; private set; }

    public DateTime ConcedidoEm { get; private set; }

    /// <summary>Quando a autorização deixa de valer sozinha.</summary>
    public DateTime ExpiraEm { get; private set; }

    /// <summary>Quando o Responsável revogou. Nulo enquanto a concessão vale.</summary>
    public DateTime? RevogadoEm { get; private set; }

    /// <summary>Por que o Responsável concedeu — segunda opinião, mudança de clínica, viagem.</summary>
    [MaxLength(300)]
    public string? Motivo { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private AcessoColmeia() { }

    /// <summary>Concede acesso ao histórico do animal (RN-090).</summary>
    public AcessoColmeia(
        Guid animalId,
        Guid tutorId,
        Guid veterinarioId,
        EscopoAcessoColmeia escopo,
        TimeSpan? validade = null,
        Guid? empresaId = null,
        string? motivo = null)
    {
        var duracao = validade ?? ValidadePadrao;

        if (duracao <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(validade), "A validade deve ser maior que zero.");

        if (duracao > ValidadeMaxima)
            throw new ArgumentOutOfRangeException(nameof(validade),
                $"A validade nao pode passar de {ValidadeMaxima.TotalDays:0} dias.");

        Id = Guid.NewGuid();
        AnimalId = animalId;
        TutorId = tutorId;
        VeterinarioId = veterinarioId;
        EmpresaId = empresaId;
        Escopo = escopo;
        Motivo = motivo;
        ConcedidoEm = DateTime.UtcNow;
        ExpiraEm = ConcedidoEm.Add(duracao);
    }

    /// <summary>
    /// Revoga a autorização (RN-062). Revogar não apaga o que já foi acessado — o log
    /// de acesso continua lá, e é isso que o Responsável precisa poder conferir.
    /// </summary>
    public void Revogar() => RevogadoEm ??= DateTime.UtcNow;

    /// <summary>Verdadeiro quando a concessão vale neste instante.</summary>
    public bool Vigente(DateTime agora) => RevogadoEm is null && ExpiraEm > agora;

    /// <summary>
    /// Adia o vencimento da autorização (RN-090).
    ///
    /// Só adia — nunca encurta, e nunca ressuscita uma autorização revogada. Encurtar
    /// por engano tiraria acesso que o Responsável concedeu; ressuscitar contornaria
    /// uma revogação, que é a decisão mais explícita que ele pode tomar. O teto de um
    /// ano continua valendo: autorização sem prazo é procuração em branco.
    /// </summary>
    public void Prorrogar(DateTime ate)
    {
        if (RevogadoEm is not null || ate <= ExpiraEm)
            return;

        var limite = ConcedidoEm.Add(ValidadeMaxima);

        ExpiraEm = ate > limite ? limite : ate;
    }

    /// <summary>Verdadeiro quando a concessão alcança o que está sendo pedido.</summary>
    public bool Alcanca(EscopoAcessoColmeia pedido) =>
        Escopo == EscopoAcessoColmeia.HistoricoCompleto || Escopo == pedido;
}

/// <summary>
/// Registro imutável de um acesso efetivamente feito pela colmeia (RN-090).
///
/// A tabela é <b>append-only</b>: não há update nem delete. Autorizar um profissional
/// a ler o histórico do animal só é aceitável se o Responsável puder ver, depois,
/// quem leu o quê e quando — a autorização sem o registro seria um cheque em branco.
/// </summary>
public class LogAcessoColmeia
{
    /// <summary>Identificador do registro (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Concessão que autorizou o acesso. Nulo quando o acesso foi negado.</summary>
    public Guid? AcessoColmeiaId { get; private set; }

    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Quem acessou.</summary>
    public Guid? VeterinarioId { get; private set; }

    /// <summary>O que foi acessado.</summary>
    [Required]
    public EscopoAcessoColmeia Escopo { get; private set; }

    /// <summary>Rota chamada — é o que dá contexto ao registro na hora de auditar.</summary>
    [MaxLength(200)]
    public string? Rota { get; private set; }

    /// <summary>
    /// Falso quando o acesso foi recusado. Tentativa negada também fica registrada:
    /// é justamente o que se quer enxergar numa auditoria.
    /// </summary>
    public bool Permitido { get; private set; }

    public DateTime OcorridoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private LogAcessoColmeia() { }

    /// <summary>Registra um acesso à colmeia. Não há como alterá-lo depois.</summary>
    public LogAcessoColmeia(
        Guid animalId,
        Guid? veterinarioId,
        EscopoAcessoColmeia escopo,
        bool permitido,
        Guid? acessoColmeiaId = null,
        string? rota = null)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        VeterinarioId = veterinarioId;
        Escopo = escopo;
        Permitido = permitido;
        AcessoColmeiaId = acessoColmeiaId;
        Rota = rota;
        OcorridoEm = DateTime.UtcNow;
    }
}
