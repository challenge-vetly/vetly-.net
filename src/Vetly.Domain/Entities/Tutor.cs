using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa o tutor (responsável) de um ou mais animais na plataforma Vetly.
/// Armazena os consentimentos LGPD de forma granular conforme exigência legal.
/// </summary>
public class Tutor
{
    /// <summary>Identificador único do tutor (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome completo do tutor.</summary>
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; private set; }

    /// <summary>E-mail do tutor. Usado para comunicações e lembretes, se consentido.</summary>
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [MaxLength(254)] // RFC 5321: limite máximo de endereço de e-mail
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; private set; }

    /// <summary>Telefone de contato do tutor (com DDD).</summary>
    [Required(ErrorMessage = "O telefone é obrigatório.")]
    [MaxLength(20)]
    public string Telefone { get; private set; }

    /// <summary>
    /// Consentimento para realização de atendimentos clínicos.
    /// Obrigatório para agendar consultas (LGPD Art. 7º).
    /// </summary>
    public bool ConsentimentoAtendimento { get; private set; }

    /// <summary>Consentimento para envio de lembretes de consulta e vacinas.</summary>
    public bool ConsentimentoLembretes { get; private set; }

    /// <summary>
    /// Consentimento para compartilhamento com clínicas parceiras da rede.
    /// É a chave que abre a colmeia (RN-064/RN-066).
    /// </summary>
    public bool ConsentimentoCompartilhamento { get; private set; }

    /// <summary>Opt-in específico de marketing. Promoções exigem consentimento próprio (RN-093).</summary>
    public bool ConsentimentoPromocoes { get; private set; }

    /// <summary>
    /// Uso dos dados em agregados anonimizados (RN-075). Tem opt-out específico,
    /// sem perda de funcionalidade no app (RN-077).
    /// </summary>
    public bool ConsentimentoDadosAgregados { get; private set; }

    // ── Datas por finalidade (RN-061/RN-062) ─────────────────────────────────
    // A LGPD exige registro de data e hora de concessão e de revogação por
    // finalidade — um único DataConsentimento não dá conta disso.

    public DateTime? DataConcessaoAtendimento { get; private set; }
    public DateTime? DataRevogacaoAtendimento { get; private set; }
    public DateTime? DataConcessaoLembretes { get; private set; }
    public DateTime? DataRevogacaoLembretes { get; private set; }
    public DateTime? DataConcessaoCompartilhamento { get; private set; }
    public DateTime? DataRevogacaoCompartilhamento { get; private set; }
    public DateTime? DataConcessaoPromocoes { get; private set; }
    public DateTime? DataRevogacaoPromocoes { get; private set; }
    public DateTime? DataConcessaoDadosAgregados { get; private set; }
    public DateTime? DataRevogacaoDadosAgregados { get; private set; }

    /// <summary>
    /// Hash da senha de acesso ao app. Nulo em cadastros feitos pelo balcão
    /// (Admin/vet), que ainda não definiram senha.
    /// </summary>
    [MaxLength(255)]
    public string? SenhaHash { get; private set; }

    /// <summary>
    /// Data e hora em que o consentimento foi registrado.
    /// Nulo enquanto o tutor não tiver registrado nenhum consentimento.
    /// </summary>
    public DateTime? DataConsentimento { get; private set; }

    /// <summary>Indica se o cadastro está ativo. Desativação é feita via soft delete.</summary>
    public bool Ativo { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Tutor()
    {
        Nome = null!;
        Email = null!;
        Telefone = null!;
    }

    /// <summary>Cria um novo tutor com os dados de contato obrigatórios.</summary>
    public Tutor(string nome, string email, string telefone)
    {
        Id = Guid.NewGuid();
        Nome = nome;
        Email = email;
        Telefone = telefone;
        Ativo = true;
    }

    /// <summary>Atualiza os dados de contato do tutor.</summary>
    public void AtualizarDados(string nome, string email, string telefone)
    {
        Nome = nome;
        Email = email;
        Telefone = telefone;
    }

    /// <summary>
    /// Registra o consentimento LGPD de forma granular.
    /// Cada campo é independente — o tutor pode consentir parcialmente.
    /// Atualiza DataConsentimento sempre que chamado.
    /// </summary>
    public void RegistrarConsentimento(bool atendimento, bool lembretes, bool compartilhamento)
    {
        var agora = DateTime.UtcNow;
        RegistrarConsentimento(FinalidadeConsentimento.Atendimento, atendimento, agora);
        RegistrarConsentimento(FinalidadeConsentimento.Lembretes, lembretes, agora);
        RegistrarConsentimento(FinalidadeConsentimento.Compartilhamento, compartilhamento, agora);
    }

    /// <summary>
    /// Concede ou revoga uma finalidade específica, registrando data e hora (RN-061/RN-062).
    /// A revogação cessa concessões futuras e <b>não apaga</b> registros clínicos já
    /// produzidos — a guarda regulatória do prontuário permanece.
    /// </summary>
    public void RegistrarConsentimento(FinalidadeConsentimento finalidade, bool concedido, DateTime quando)
    {
        switch (finalidade)
        {
            case FinalidadeConsentimento.Atendimento:
                ConsentimentoAtendimento = concedido;
                if (concedido) DataConcessaoAtendimento = quando; else DataRevogacaoAtendimento = quando;
                break;
            case FinalidadeConsentimento.Lembretes:
                ConsentimentoLembretes = concedido;
                if (concedido) DataConcessaoLembretes = quando; else DataRevogacaoLembretes = quando;
                break;
            case FinalidadeConsentimento.Compartilhamento:
                ConsentimentoCompartilhamento = concedido;
                if (concedido) DataConcessaoCompartilhamento = quando; else DataRevogacaoCompartilhamento = quando;
                break;
            case FinalidadeConsentimento.Promocoes:
                ConsentimentoPromocoes = concedido;
                if (concedido) DataConcessaoPromocoes = quando; else DataRevogacaoPromocoes = quando;
                break;
            case FinalidadeConsentimento.DadosAgregados:
                ConsentimentoDadosAgregados = concedido;
                if (concedido) DataConcessaoDadosAgregados = quando; else DataRevogacaoDadosAgregados = quando;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(finalidade), finalidade, "Finalidade de consentimento desconhecida.");
        }

        DataConsentimento = quando;
    }

    /// <summary>Verifica se uma finalidade está autorizada neste momento.</summary>
    public bool Consentiu(FinalidadeConsentimento finalidade) => finalidade switch
    {
        FinalidadeConsentimento.Atendimento => ConsentimentoAtendimento,
        FinalidadeConsentimento.Lembretes => ConsentimentoLembretes,
        FinalidadeConsentimento.Compartilhamento => ConsentimentoCompartilhamento,
        FinalidadeConsentimento.Promocoes => ConsentimentoPromocoes,
        FinalidadeConsentimento.DadosAgregados => ConsentimentoDadosAgregados,
        _ => false
    };

    /// <summary>
    /// Estado de todas as finalidades, com data de concessão e de revogação —
    /// é o que o Responsável enxerga no app (RN-061).
    /// </summary>
    public IReadOnlyList<ConsentimentoRegistrado> Consentimentos() =>
    [
        new(FinalidadeConsentimento.Atendimento, ConsentimentoAtendimento, DataConcessaoAtendimento, DataRevogacaoAtendimento),
        new(FinalidadeConsentimento.Lembretes, ConsentimentoLembretes, DataConcessaoLembretes, DataRevogacaoLembretes),
        new(FinalidadeConsentimento.Compartilhamento, ConsentimentoCompartilhamento, DataConcessaoCompartilhamento, DataRevogacaoCompartilhamento),
        new(FinalidadeConsentimento.Promocoes, ConsentimentoPromocoes, DataConcessaoPromocoes, DataRevogacaoPromocoes),
        new(FinalidadeConsentimento.DadosAgregados, ConsentimentoDadosAgregados, DataConcessaoDadosAgregados, DataRevogacaoDadosAgregados)
    ];

    /// <summary>
    /// Define o hash da senha de acesso ao app. O hash é produzido na camada de
    /// infraestrutura — a entidade nunca vê a senha em claro.
    /// </summary>
    public void DefinirSenhaHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("O hash da senha é obrigatório.", nameof(hash));

        SenhaHash = hash;
    }

    /// <summary>Verdadeiro quando o Responsável já pode entrar pelo app com senha.</summary>
    public bool TemCredencial() => !string.IsNullOrWhiteSpace(SenhaHash);

    /// <summary>Desativa o cadastro do tutor (soft delete).</summary>
    public void Desativar() => Ativo = false;
}
