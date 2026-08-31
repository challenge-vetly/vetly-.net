using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Avaliação do atendimento pelo Responsável (RN-055/RN-057).
///
/// Só avalia quem foi atendido, e só uma vez por consulta. É o que separa reputação
/// de campanha: sem o vínculo com um atendimento pago e realizado, a nota vira
/// número que qualquer um pode empurrar para cima ou para baixo.
///
/// A nota não é editável depois de enviada — corrigir uma avaliação seria abrir a
/// porta para pressão sobre quem avaliou. O comentário, por outro lado, pode ser
/// moderado, porque texto ofensivo é problema de outra natureza.
/// </summary>
public class Avaliacao
{
    /// <summary>
    /// Prazo para avaliar depois do atendimento: 14 dias (RN-055).
    ///
    /// É a janela do Airbnb, referência de marketplace bilateral com reputação.
    /// Avaliação muito posterior mede memória, não atendimento.
    /// </summary>
    public static readonly TimeSpan PrazoParaAvaliar = TimeSpan.FromDays(14);

    /// <summary>Identificador da avaliação (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Consulta avaliada. Há no máximo uma avaliação por consulta.</summary>
    [Required]
    public Guid ConsultaId { get; private set; }

    /// <summary>Responsável que avaliou.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>Veterinário avaliado.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Clínica, quando o atendimento foi por uma.</summary>
    public Guid? EmpresaId { get; private set; }

    /// <summary>Nota de 1 a 5.</summary>
    [Range(1, 5)]
    public int Nota { get; private set; }

    /// <summary>Comentário livre do Responsável. Opcional.</summary>
    [MaxLength(1000)]
    public string? Comentario { get; private set; }

    /// <summary>
    /// Comentário escondido por moderação. A nota permanece contando: esconder o texto
    /// não pode virar um jeito de apagar uma avaliação ruim.
    /// </summary>
    public bool ComentarioModerado { get; private set; }

    /// <summary>Motivo da moderação, para a trilha da operação.</summary>
    [MaxLength(300)]
    public string? MotivoDaModeracao { get; private set; }

    /// <summary>Resposta pública do veterinário à avaliação.</summary>
    [MaxLength(1000)]
    public string? RespostaDoVeterinario { get; private set; }

    public DateTime? RespondidaEm { get; private set; }

    /// <summary>
    /// Falso quando a consulta avaliada foi cancelada ou reembolsada (RN-059). A
    /// avaliação some do cálculo da nota, mas a linha permanece: apagar registro de
    /// reputação abriria caminho para gestão de nota via cancelamento.
    /// </summary>
    public bool Valida { get; private set; }

    /// <summary>Por que a avaliação foi invalidada, quando foi.</summary>
    [MaxLength(200)]
    public string? MotivoDaInvalidacao { get; private set; }

    public DateTime CriadaEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Avaliacao() { }

    /// <summary>Registra a avaliação de um atendimento (RN-055).</summary>
    public Avaliacao(
        Guid consultaId,
        Guid tutorId,
        Guid veterinarioId,
        int nota,
        string? comentario = null,
        Guid? empresaId = null)
    {
        if (nota is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(nota), "A nota deve estar entre 1 e 5.");

        Id = Guid.NewGuid();
        ConsultaId = consultaId;
        TutorId = tutorId;
        VeterinarioId = veterinarioId;
        EmpresaId = empresaId;
        Nota = nota;
        Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();
        Valida = true;
        CriadaEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Esconde o comentário por moderação. A nota continua contando na média — o
    /// contrário transformaria a moderação em ferramenta para apagar crítica.
    /// </summary>
    public void ModerarComentario(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("A moderação exige motivo.", nameof(motivo));

        ComentarioModerado = true;
        MotivoDaModeracao = motivo.Trim();
    }

    /// <summary>
    /// Registra a resposta pública do veterinário. Uma só: a avaliação é do
    /// Responsável, e a réplica não vira debate no perfil.
    /// </summary>
    public void Responder(string resposta)
    {
        if (string.IsNullOrWhiteSpace(resposta))
            throw new ArgumentException("A resposta não pode ser vazia.", nameof(resposta));

        if (RespondidaEm is not null)
            throw new InvalidOperationException("Esta avaliação já foi respondida.");

        RespostaDoVeterinario = resposta.Trim();
        RespondidaEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Invalida a avaliação de uma consulta cancelada ou reembolsada (RN-059).
    ///
    /// Sai do cálculo da nota, mas a linha fica. A diferença importa: apagar o
    /// registro permitiria a um prestador limpar uma avaliação ruim provocando o
    /// cancelamento, e a auditoria não teria como notar.
    /// </summary>
    public void Invalidar(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("A invalidação exige motivo.", nameof(motivo));

        Valida = false;
        MotivoDaInvalidacao = motivo.Trim();
    }

    /// <summary>O comentário visível ao público — nulo quando foi moderado.</summary>
    public string? ComentarioPublico() => ComentarioModerado ? null : Comentario;
}
