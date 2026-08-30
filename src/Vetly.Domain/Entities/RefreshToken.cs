using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Refresh token rotativo emitido no login (§2.2 do documento de engenharia).
///
/// Guarda apenas o <b>hash</b> do token: vazamento da tabela não permite se passar
/// pelo usuário. Cada uso rotaciona — o token antigo é revogado e aponta para o que
/// o substituiu, o que torna reuso de token detectável.
/// </summary>
public class RefreshToken
{
    /// <summary>Identificador único do refresh token (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Id do usuário dono do token (tutor, veterinário ou administrador).</summary>
    [Required]
    public Guid UsuarioId { get; private set; }

    /// <summary>Tipo do usuário, que define a role reemitida no refresh.</summary>
    [Required]
    public TipoUsuario TipoUsuario { get; private set; }

    /// <summary>Hash SHA-256 do token entregue ao cliente. O valor em claro nunca é persistido.</summary>
    [Required]
    [MaxLength(64)]
    public string Hash { get; private set; }

    /// <summary>Data e hora de emissão (UTC).</summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>Data e hora de expiração (UTC).</summary>
    public DateTime ExpiraEm { get; private set; }

    /// <summary>Indica se o token foi revogado — por uso, logout ou offboarding (RN-022).</summary>
    public bool Revogado { get; private set; }

    /// <summary>Quando a revogação aconteceu.</summary>
    public DateTime? RevogadoEm { get; private set; }

    /// <summary>
    /// Token que substituiu este na rotação. Preenchido no uso; permite rastrear a
    /// cadeia e identificar reuso de token já rotacionado.
    /// </summary>
    public Guid? SubstituidoPorId { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private RefreshToken() => Hash = null!;

    /// <summary>Cria um refresh token para o usuário informado.</summary>
    public RefreshToken(Guid usuarioId, TipoUsuario tipoUsuario, string hash, DateTime expiraEm)
    {
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("O hash do refresh token é obrigatório.", nameof(hash));

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        TipoUsuario = tipoUsuario;
        Hash = hash;
        CriadoEm = DateTime.UtcNow;
        ExpiraEm = expiraEm;
    }

    /// <summary>Verdadeiro quando o token ainda pode ser usado no momento informado.</summary>
    public bool EstaValido(DateTime agora) => !Revogado && ExpiraEm > agora;

    /// <summary>
    /// Revoga o token. <paramref name="substituidoPorId"/> registra a rotação quando a
    /// revogação decorre do uso legítimo.
    /// </summary>
    public void Revogar(DateTime quando, Guid? substituidoPorId = null)
    {
        Revogado = true;
        RevogadoEm = quando;
        SubstituidoPorId = substituidoPorId;
    }
}
