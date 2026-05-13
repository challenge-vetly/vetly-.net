using Vetly.Application.DTOs.Internacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de internacoes. Gerencia abertura, apuracao de valor diario e alta.
/// </summary>
public class InternacaoService : IInternacaoService
{
    private readonly IInternacaoRepository _repo;

    public InternacaoService(IInternacaoRepository repo) => _repo = repo;

    public async Task<IEnumerable<InternacaoDto>> ObterTodosAsync()
    {
        var internacoes = await _repo.ObterTodosAsync();
        return internacoes.Select(MapearParaDto);
    }

    public async Task<InternacaoDto> ObterPorIdAsync(Guid id)
    {
        var internacao = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Internacao", id);
        return MapearParaDto(internacao);
    }

    public async Task<InternacaoDto> AbrirAsync(CriarInternacaoDto dto)
    {
        // Verifica se o animal ja possui uma internacao ativa
        var ativa = await _repo.ObterAtivaDoAnimalAsync(dto.AnimalId);
        if (ativa is not null)
            throw new BusinessRuleException("INTERNACAO-001",
                "O animal ja possui uma internacao ativa. Finalize a atual antes de abrir outra.");

        var internacao = new Internacao(dto.AnimalId, dto.VeterinarioId, dto.ValorCaucao);
        await _repo.AdicionarAsync(internacao);
        await _repo.SalvarAsync();
        return MapearParaDto(internacao);
    }

    public async Task AtualizarAsync(Guid id, string procedimentosJson)
    {
        var internacao = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Internacao", id);
        internacao.RegistrarProcedimentoDiario(procedimentosJson);
        _repo.Atualizar(internacao);
        await _repo.SalvarAsync();
    }

    public async Task<InternacaoDto> DarAltaAsync(Guid id)
    {
        var internacao = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Internacao", id);

        internacao.DarAlta();
        _repo.Atualizar(internacao);
        await _repo.SalvarAsync();
        return MapearParaDto(internacao);
    }

    private static InternacaoDto MapearParaDto(Internacao i) => new()
    {
        Id = i.Id, AnimalId = i.AnimalId, VeterinarioId = i.VeterinarioId,
        ValorCaucao = i.ValorCaucao, ValorTotalApurado = i.ValorTotalApurado,
        DataAbertura = i.DataAbertura, DataAlta = i.DataAlta,
        EstaAtiva = i.EstaAtiva(), DiasInternado = i.DiasInternado(),
        ProcedimentosDiarios = i.ProcedimentosDiarios
    };
}
