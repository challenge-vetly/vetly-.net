using System.ComponentModel.DataAnnotations;

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

    /// <summary>Consentimento para compartilhamento de dados com parceiros.</summary>
    public bool ConsentimentoCompartilhamento { get; private set; }

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
        ConsentimentoAtendimento = atendimento;
        ConsentimentoLembretes = lembretes;
        ConsentimentoCompartilhamento = compartilhamento;
        DataConsentimento = DateTime.UtcNow;
    }

    /// <summary>Desativa o cadastro do tutor (soft delete).</summary>
    public void Desativar() => Ativo = false;
}
