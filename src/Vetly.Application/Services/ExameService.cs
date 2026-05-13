using Vetly.Application.DTOs.Exame;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>Servico de exames. Gerencia solicitacao, resultado e liberacao ao tutor.</summary>
public class ExameService : IExameService
{
    private readonly IExameRepository _repo;

    public ExameService(IExameRepository repo) => _repo = repo;

    public async Task<IEnumerable<ExameDto>> ObterTodosAsync()
    {
        var exames = await _repo.ObterTodosAsync();
        return exames.Select(MapearParaDto);
    }

    public async Task<ExameDto> ObterPorIdAsync(Guid id)
    {
        var exame = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Exame", id);
        return MapearParaDto(exame);
    }

    public async Task<ExameDto> CriarAsync(CriarExameDto dto)
    {
        var exame = new Exame(dto.AnimalId, dto.VeterinarioId, dto.TipoSolicitacao);
        await _repo.AdicionarAsync(exame);
        await _repo.SalvarAsync();
        return MapearParaDto(exame);
    }

    public async Task<ExameDto> RegistrarResultadoAsync(Guid id, string resultado)
    {
        var exame = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Exame", id);
        exame.RegistrarResultado(resultado);
        _repo.Atualizar(exame);
        await _repo.SalvarAsync();
        return MapearParaDto(exame);
    }

    public async Task LiberarAoTutorAsync(Guid id)
    {
        var exame = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Exame", id);
        exame.LiberarAoTutor(); // lanca InvalidOperationException se sem resultado
        _repo.Atualizar(exame);
        await _repo.SalvarAsync();
    }

    private static ExameDto MapearParaDto(Exame e) => new()
    {
        Id = e.Id, AnimalId = e.AnimalId, VeterinarioId = e.VeterinarioId,
        TipoSolicitacao = e.TipoSolicitacao, Resultado = e.Resultado,
        LiberadoAoTutor = e.LiberadoAoTutor,
        DataSolicitacao = e.DataSolicitacao, DataResultado = e.DataResultado
    };
}
