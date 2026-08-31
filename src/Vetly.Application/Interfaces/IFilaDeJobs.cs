using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Enfileira trabalhos de negócio para rodar fora do ciclo da requisição (§11).
///
/// O serviço que enfileira não sabe quem executa nem quando: ele registra a intenção
/// e devolve a resposta ao usuário sem esperar.
/// </summary>
public interface IFilaDeJobs
{
    /// <summary>Agenda um trabalho, opcionalmente com atraso.</summary>
    Task EnfileirarAsync(TipoJob tipo, string? payload = null, TimeSpan? atraso = null);

    /// <summary>Trabalhos que já podem rodar, do mais antigo para o mais novo.</summary>
    Task<IReadOnlyList<Job>> ObterElegiveisAsync(DateTime agora, int limite);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task<int> SalvarAsync();
}

/// <summary>
/// Executa um tipo de trabalho da fila (§11).
///
/// Cada tipo de job entra como um handler novo, registrado no DI — o worker não
/// precisa conhecer nenhum deles.
/// </summary>
public interface IJobHandler
{
    /// <summary>Tipo de trabalho que este handler executa.</summary>
    TipoJob Tipo { get; }

    /// <summary>Executa o trabalho. Exceção lançada aqui conta como falha e é retentada.</summary>
    Task ExecutarAsync(Job job, CancellationToken cancellationToken);
}

/// <summary>
/// Rotina de manutenção que roda a cada ciclo do worker, sem precisar de linha na
/// fila (§11).
///
/// É o lugar do que é varredura por natureza — expirar locks vencidos, limpar
/// registros de idempotência —, que ficaria absurdo modelar como um job por item.
/// </summary>
public interface IRotinaPeriodica
{
    /// <summary>Nome da rotina, para log.</summary>
    string Nome { get; }

    /// <summary>Intervalo mínimo entre execuções.</summary>
    TimeSpan Intervalo { get; }

    /// <summary>Executa a varredura. Devolve quantos itens foram afetados, para log.</summary>
    Task<int> ExecutarAsync(CancellationToken cancellationToken);
}
