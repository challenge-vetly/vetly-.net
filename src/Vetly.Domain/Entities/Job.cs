using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Trabalho de negócio agendado para rodar fora do ciclo da requisição (§11).
///
/// Usa o Oracle que já existe, sem broker novo: no volume do MVP, uma tabela e um
/// <c>BackgroundService</c> no mesmo host resolvem. Se o volume exigir, trocar por
/// Hangfire ou Quartz não muda os handlers.
/// </summary>
public class Job
{
    /// <summary>Quantas vezes um job é tentado antes de desistir.</summary>
    public const int MaximoDeTentativas = 3;

    /// <summary>Identificador do job (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>O que deve ser feito.</summary>
    [Required]
    public TipoJob Tipo { get; private set; }

    /// <summary>Dados de entrada do handler, em JSON.</summary>
    [MaxLength(2000)]
    public string? Payload { get; private set; }

    /// <summary>A partir de quando pode executar (UTC).</summary>
    public DateTime ExecutarEm { get; private set; }

    /// <summary>Quantas execuções já foram tentadas.</summary>
    public int Tentativas { get; private set; }

    /// <summary>Situação atual.</summary>
    [Required]
    public EstadoJob Estado { get; private set; }

    /// <summary>Último erro registrado, quando houve falha.</summary>
    [MaxLength(1000)]
    public string? UltimoErro { get; private set; }

    public DateTime CriadoEm { get; private set; }

    public DateTime? ConcluidoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Job() { }

    /// <summary>Enfileira um trabalho, opcionalmente com atraso.</summary>
    public Job(TipoJob tipo, string? payload = null, TimeSpan? atraso = null)
    {
        Id = Guid.NewGuid();
        Tipo = tipo;
        Payload = payload;
        CriadoEm = DateTime.UtcNow;
        ExecutarEm = CriadoEm.Add(atraso ?? TimeSpan.Zero);
        Estado = EstadoJob.Pendente;
    }

    /// <summary>Marca a conclusão com sucesso.</summary>
    public void Concluir()
    {
        Estado = EstadoJob.Concluido;
        ConcluidoEm = DateTime.UtcNow;
        Tentativas++;
    }

    /// <summary>
    /// Registra uma falha. Enquanto houver tentativa sobrando, reagenda com espera
    /// crescente; esgotadas, desiste — repetir para sempre um job que sempre falha só
    /// consumiria o worker.
    /// </summary>
    public void RegistrarFalha(string erro, DateTime agora)
    {
        Tentativas++;
        UltimoErro = erro.Length > 1000 ? erro[..1000] : erro;

        if (Tentativas >= MaximoDeTentativas)
        {
            Estado = EstadoJob.Falhou;
            ConcluidoEm = agora;
            return;
        }

        // Espera crescente: 30s, 90s
        ExecutarEm = agora.AddSeconds(30 * Math.Pow(3, Tentativas - 1));
    }

    /// <summary>Verdadeiro quando o job já pode rodar.</summary>
    public bool Elegivel(DateTime agora) => Estado == EstadoJob.Pendente && ExecutarEm <= agora;
}
