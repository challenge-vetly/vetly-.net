using System.ComponentModel.DataAnnotations;

namespace Vetly.Domain.Entities;

/// <summary>
/// Resposta guardada de uma requisição idempotente (§2.5).
///
/// Existe porque as operações que movimentam dinheiro ou reservam horário não podem
/// acontecer duas vezes quando o app repete o envio — e o app repete: rede oscila,
/// usuário toca de novo, cliente faz retry automático.
///
/// A chave é o trio <c>(chave, usuário, rota)</c>: a mesma chave enviada por outra
/// pessoa, ou na mesma pessoa para outra rota, é outra requisição.
/// </summary>
public class RegistroIdempotencia
{
    /// <summary>Por quanto tempo a resposta guardada vale (§2.5).</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    /// <summary>Identificador do registro (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Chave enviada pelo cliente no header <c>Idempotency-Key</c>.</summary>
    [Required]
    [MaxLength(100)]
    public string Chave { get; private set; }

    /// <summary>Usuário que fez a requisição.</summary>
    [Required]
    public Guid UsuarioId { get; private set; }

    /// <summary>Rota chamada, no formato <c>MÉTODO caminho</c>.</summary>
    [Required]
    [MaxLength(200)]
    public string Rota { get; private set; }

    /// <summary>Status HTTP devolvido na primeira execução.</summary>
    public int StatusHttp { get; private set; }

    /// <summary>Corpo da resposta da primeira execução, em JSON.</summary>
    public string? Resposta { get; private set; }

    /// <summary>Quando a requisição foi processada.</summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>Até quando esta resposta é reaproveitada.</summary>
    public DateTime ExpiraEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private RegistroIdempotencia()
    {
        Chave = null!;
        Rota = null!;
    }

    /// <summary>Guarda a resposta de uma requisição idempotente.</summary>
    public RegistroIdempotencia(string chave, Guid usuarioId, string rota, int statusHttp, string? resposta)
    {
        if (string.IsNullOrWhiteSpace(chave))
            throw new ArgumentException("A chave de idempotência é obrigatória.", nameof(chave));

        Id = Guid.NewGuid();
        Chave = chave;
        UsuarioId = usuarioId;
        Rota = rota;
        StatusHttp = statusHttp;
        Resposta = resposta;
        CriadoEm = DateTime.UtcNow;
        ExpiraEm = CriadoEm.Add(Validade);
    }

    /// <summary>Verdadeiro enquanto a resposta guardada ainda pode ser reaproveitada.</summary>
    public bool Vigente(DateTime agora) => ExpiraEm > agora;
}
