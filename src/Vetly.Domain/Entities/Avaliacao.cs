using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.Domain.Entities;

/// <summary>
/// Avaliação de uma consulta realizada (RN-076..082). Única por consulta — a unicidade
/// é reforçada pelo repositório/índice, não por esta entidade. Nasce com o gatilho
/// "vet marcou como realizada" e tem uma janela de 7 dias para ser criada e de 48h
/// (a partir da própria publicação) para ser editada.
/// </summary>
public class Avaliacao
{
    /// <summary>Identificador único da avaliação (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id da consulta avaliada. Única por consulta (RN-076).</summary>
    public Guid ConsultaId { get; private set; }

    /// <summary>Id do responsável autor da avaliação.</summary>
    public Guid ResponsavelId { get; private set; }

    /// <summary>Id do veterinário avaliado.</summary>
    public Guid VeterinarioId { get; private set; }

    /// <summary>Nota geral (1-5). Única obrigatória no MVP (RN-077).</summary>
    [Range(1, 5)]
    public int NotaGeral { get; private set; }

    /// <summary>Subnota opcional de atendimento (1-5).</summary>
    public int? NotaAtendimento { get; private set; }

    /// <summary>Subnota opcional de pontualidade (1-5).</summary>
    public int? NotaPontualidade { get; private set; }

    /// <summary>Subnota opcional de estrutura (1-5).</summary>
    public int? NotaEstrutura { get; private set; }

    /// <summary>Subnota opcional de custo-benefício (1-5).</summary>
    public int? NotaCustoBeneficio { get; private set; }

    /// <summary>Comentário opcional em texto livre.</summary>
    [MaxLength(2000)]
    public string? Comentario { get; private set; }

    /// <summary>Momento de publicação da avaliação. Abre a janela de edição de 48h (RN-082).</summary>
    public DateTime Data { get; private set; }

    /// <summary>Status de moderação do comentário (RN-080). A nota nunca é afetada pela moderação.</summary>
    public StatusModeracao StatusModeracao { get; private set; }

    /// <summary>Resposta pública do veterinário (0..1 — RN-079).</summary>
    [MaxLength(2000)]
    public string? RespostaVeterinario { get; private set; }

    /// <summary>Momento em que o veterinário respondeu, se houver.</summary>
    public DateTime? DataResposta { get; private set; }

    /// <summary>
    /// True quando a avaliação foi invalidada por antifraude (consulta cancelada/reembolsada
    /// após a avaliação — RN-081). Nunca entra em médias/exibição pública quando true.
    /// </summary>
    public bool Invalidada { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Avaliacao() { }

    private Avaliacao(
        Guid consultaId, Guid responsavelId, Guid veterinarioId,
        int notaGeral, int? notaAtendimento, int? notaPontualidade,
        int? notaEstrutura, int? notaCustoBeneficio, string? comentario, DateTime agora)
    {
        Id = Guid.NewGuid();
        ConsultaId = consultaId;
        ResponsavelId = responsavelId;
        VeterinarioId = veterinarioId;
        NotaGeral = ValidarNota(notaGeral);
        NotaAtendimento = notaAtendimento is { } na ? ValidarNota(na) : null;
        NotaPontualidade = notaPontualidade is { } np ? ValidarNota(np) : null;
        NotaEstrutura = notaEstrutura is { } ne ? ValidarNota(ne) : null;
        NotaCustoBeneficio = notaCustoBeneficio is { } ncb ? ValidarNota(ncb) : null;
        Comentario = comentario;
        Data = agora;
        StatusModeracao = StatusModeracao.Publicada;
    }

    /// <summary>
    /// Cria a avaliação de uma consulta já realizada, validando o gatilho (RN-076: só após
    /// "realizada") e a janela de 7 dias a partir de <paramref name="dataRealizada"/>
    /// (RN-076). Método-fábrica: quem chama já buscou a consulta e sabe seu estado —
    /// mantém a checagem de transição/janela dentro do domínio, como o resto da base.
    /// </summary>
    public static Avaliacao Criar(
        Guid consultaId, Guid responsavelId, Guid veterinarioId,
        StatusConsulta statusConsulta, DateTime? dataRealizada,
        int notaGeral, int? notaAtendimento, int? notaPontualidade,
        int? notaEstrutura, int? notaCustoBeneficio, string? comentario, DateTime agora)
    {
        if (statusConsulta != StatusConsulta.Realizada || dataRealizada is null)
            throw new DomainException("AVALIACAO-002",
                "Só é possível avaliar consultas marcadas como realizadas.");

        if (agora > dataRealizada.Value.AddDays(7))
            throw new DomainException("AVALIACAO-001",
                "O prazo de 7 dias para avaliar esta consulta expirou.");

        return new Avaliacao(
            consultaId, responsavelId, veterinarioId, notaGeral, notaAtendimento,
            notaPontualidade, notaEstrutura, notaCustoBeneficio, comentario, agora);
    }

    /// <summary>
    /// Edita a avaliação (RN-082): só é permitido dentro de 48h da publicação original.
    /// Reabrir a moderação não é escopo desta chamada — uma edição sempre republica como
    /// <see cref="StatusModeracao.Publicada"/>.
    /// </summary>
    public void Editar(
        int notaGeral, int? notaAtendimento, int? notaPontualidade,
        int? notaEstrutura, int? notaCustoBeneficio, string? comentario, DateTime agora)
    {
        if (agora > Data.AddHours(48))
            throw new DomainException("AVALIACAO-003",
                "A avaliação só pode ser editada em até 48h após a publicação.");

        NotaGeral = ValidarNota(notaGeral);
        NotaAtendimento = notaAtendimento is { } na ? ValidarNota(na) : null;
        NotaPontualidade = notaPontualidade is { } np ? ValidarNota(np) : null;
        NotaEstrutura = notaEstrutura is { } ne ? ValidarNota(ne) : null;
        NotaCustoBeneficio = notaCustoBeneficio is { } ncb ? ValidarNota(ncb) : null;
        Comentario = comentario;
        StatusModeracao = StatusModeracao.Publicada;
    }

    /// <summary>Registra a resposta pública do veterinário (RN-079). Só uma por avaliação.</summary>
    public void Responder(string resposta, DateTime agora)
    {
        if (RespostaVeterinario is not null)
            throw new DomainException("AVALIACAO-004",
                "O veterinário já respondeu esta avaliação.");

        RespostaVeterinario = resposta;
        DataResposta = agora;
    }

    /// <summary>
    /// Aplica moderação ao comentário (RN-080): oculta ou republica o texto. A nota geral
    /// nunca é alterada por este método — moderação nunca é ferramenta de gestão de nota.
    /// </summary>
    public void Moderar(StatusModeracao status) => StatusModeracao = status;

    /// <summary>
    /// Invalida a avaliação por antifraude (RN-081): consulta cuja avaliação já existia
    /// foi cancelada/reembolsada depois. Avaliação invalidada nunca entra em médias.
    /// </summary>
    public void Invalidar() => Invalidada = true;

    private static int ValidarNota(int nota)
    {
        if (nota is < 1 or > 5)
            throw new DomainException("AVALIACAO-005", "A nota deve estar entre 1 e 5.");
        return nota;
    }
}
