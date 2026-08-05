using Vetly.Application.DTOs.Obrigacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Factories;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço do calendário de obrigações do pet. Gera o calendário via Factory por espécie
/// (RN-069) — mesmo padrão de seleção de <see cref="IDocumentoFactory"/>.
/// </summary>
public class ObrigacaoService : IObrigacaoService
{
    private readonly IObrigacaoDoPetRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IEnumerable<IObrigacaoFactory> _factories;
    private readonly TimeProvider _timeProvider;

    public ObrigacaoService(
        IObrigacaoDoPetRepository repo, IAnimalRepository animalRepo,
        IEnumerable<IObrigacaoFactory> factories, TimeProvider timeProvider)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _factories = factories;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ObrigacaoDoPetDto>> GerarCalendarioAsync(Guid animalId)
    {
        var animal = await _animalRepo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        if (await _repo.ExisteCalendarioAsync(animalId))
            throw new BusinessRuleException("OBRIGACAO-002",
                "O calendário de obrigações já foi gerado para este animal.");

        var factory = _factories.First(f => f.Aplicavel(animal.Especie));
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var obrigacoes = factory.GerarCalendario(animalId, agora).ToList();

        foreach (var obrigacao in obrigacoes)
            await _repo.AdicionarAsync(obrigacao);
        await _repo.SalvarAsync();

        return obrigacoes.Select(o => MapearParaDto(o, agora));
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ObrigacaoDoPetDto>> ObterPorAnimalAsync(Guid animalId)
    {
        _ = await _animalRepo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        var obrigacoes = await _repo.ObterPorAnimalAsync(animalId);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        return obrigacoes.Select(o => MapearParaDto(o, agora));
    }

    private static ObrigacaoDoPetDto MapearParaDto(ObrigacaoDoPet o, DateTime agora) => new()
    {
        Id = o.Id, AnimalId = o.AnimalId, Tipo = o.Tipo, DataLimite = o.DataLimite,
        Status = o.Status, ConsultaId = o.ConsultaId, DataCumprimento = o.DataCumprimento,
        Atrasada = o.EstaAtrasada(agora)
    };
}
