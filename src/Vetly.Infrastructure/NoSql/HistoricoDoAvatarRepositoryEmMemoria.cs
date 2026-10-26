using System.Collections.Concurrent;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.NoSql;

/// <summary>
/// Histórico do avatar em memória (<c>Adaptadores:DocumentoNoSql = Memoria</c>, o
/// padrão).
///
/// É o que deixa a API inteira — e a suíte de testes — rodar sem um MongoDB de pé,
/// do mesmo jeito que o STT e o pagamento têm adaptador simulado. Registrado como
/// singleton: o histórico vive enquanto o processo vive e some no restart, o que é
/// aceitável em desenvolvimento e é exatamente por isso que produção usa o Mongo.
/// </summary>
public sealed class HistoricoDoAvatarRepositoryEmMemoria : IHistoricoDoAvatarRepository
{
    private readonly ConcurrentDictionary<(Guid AnimalId, DateOnly Data), RegistroDiarioDoAvatarDto> _registros = new();

    /// <inheritdoc/>
    public Task RegistrarAsync(RegistroDiarioDoAvatarDto registro, CancellationToken cancellationToken = default)
    {
        // Mesma regra do indice unico do Mongo: um por animal por dia, o ultimo vence
        _registros[(registro.AnimalId, registro.Data)] = Copiar(registro);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ResultadoPaginado<RegistroDiarioDoAvatarDto>> ObterPaginadoAsync(
        Guid animalId, Paginacao paginacao, CriterioDeOrdenacao ordenacao, CancellationToken cancellationToken = default)
    {
        var doAnimal = _registros.Values.Where(r => r.AnimalId == animalId).ToList();

        var ordenados = (ordenacao.Campo, ordenacao.Descendente) switch
        {
            (CamposDeOrdenacao.HistoricoDoAvatar.Saude, false) => doAnimal.OrderBy(r => r.Saude).ThenByDescending(r => r.Data),
            (CamposDeOrdenacao.HistoricoDoAvatar.Saude, true) => doAnimal.OrderByDescending(r => r.Saude).ThenByDescending(r => r.Data),
            (CamposDeOrdenacao.HistoricoDoAvatar.Humor, false) => doAnimal.OrderBy(r => r.Humor).ThenByDescending(r => r.Data),
            (CamposDeOrdenacao.HistoricoDoAvatar.Humor, true) => doAnimal.OrderByDescending(r => r.Humor).ThenByDescending(r => r.Data),
            (_, false) => doAnimal.OrderBy(r => r.Data),
            (_, true) => doAnimal.OrderByDescending(r => r.Data)
        };

        var pagina = ordenados.Skip(paginacao.Deslocamento).Take(paginacao.Tamanho).Select(Copiar).ToList();

        return Task.FromResult(new ResultadoPaginado<RegistroDiarioDoAvatarDto>(pagina, doAnimal.Count, paginacao));
    }

    /// <summary>
    /// Cópia na entrada e na saída: sem ela, quem recebeu o registro poderia alterá-lo
    /// e mudar o "banco" — comportamento que o Mongo nunca teria.
    /// </summary>
    private static RegistroDiarioDoAvatarDto Copiar(RegistroDiarioDoAvatarDto r) => new()
    {
        AnimalId = r.AnimalId,
        Data = r.Data,
        Saude = r.Saude,
        Humor = r.Humor,
        Estado = r.Estado,
        NivelDoPelo = r.NivelDoPelo,
        Motivos = [.. r.Motivos],
        CalculadoEm = r.CalculadoEm
    };
}
