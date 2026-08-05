using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Responsavel;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>Servico de responsaveis. Gerencia cadastro e consentimentos LGPD.</summary>
public class ResponsavelService : IResponsavelService
{
    private readonly IResponsavelRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly TimeProvider _timeProvider;

    public ResponsavelService(IResponsavelRepository repo, IAnimalRepository animalRepo, TimeProvider timeProvider)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _timeProvider = timeProvider;
    }

    public async Task<IEnumerable<ResponsavelDto>> ObterTodosAsync()
    {
        var responsaveis = await _repo.ObterAtivosAsync();
        return responsaveis.Select(MapearParaDto);
    }

    public async Task<ResponsavelDto> ObterPorIdAsync(Guid id)
    {
        var responsavel = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Responsavel", id);
        return MapearParaDto(responsavel);
    }

    public async Task<IEnumerable<AnimalDto>> ObterAnimaisAsync(Guid responsavelId)
    {
        _ = await _repo.ObterPorIdAsync(responsavelId)
            ?? throw new NotFoundException("Responsavel", responsavelId);

        var animais = await _animalRepo.ObterPorResponsavelAsync(responsavelId);
        return animais.Select(a => new AnimalDto
        {
            Id = a.Id, Nome = a.Nome, Especie = a.Especie, Raca = a.Raca,
            DataNascimento = a.DataNascimento, IdadeEmAnos = a.IdadeEmAnos(),
            ResponsavelId = a.ResponsavelId, AlertasAtivos = a.AlertasAtivos, Ativo = a.Ativo
        });
    }

    public async Task<ResponsavelDto> CriarAsync(CriarResponsavelDto dto)
    {
        var existente = await _repo.ObterPorEmailAsync(dto.Email);
        if (existente is not null)
            throw new BusinessRuleException("RESPONSAVEL-001", $"E-mail '{dto.Email}' ja esta cadastrado.");

        var responsavel = new Responsavel(dto.Nome, dto.Email, dto.Telefone);
        await _repo.AdicionarAsync(responsavel);
        await _repo.SalvarAsync();
        return MapearParaDto(responsavel);
    }

    public async Task AtualizarAsync(Guid id, CriarResponsavelDto dto)
    {
        var responsavel = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Responsavel", id);
        responsavel.AtualizarDados(dto.Nome, dto.Email, dto.Telefone);
        _repo.Atualizar(responsavel);
        await _repo.SalvarAsync();
    }

    public async Task DesativarAsync(Guid id)
    {
        var responsavel = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Responsavel", id);
        responsavel.Desativar();
        _repo.Atualizar(responsavel);
        await _repo.SalvarAsync();
    }

    private ResponsavelDto MapearParaDto(Responsavel t)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        return new ResponsavelDto
        {
            Id = t.Id, Nome = t.Nome, Email = t.Email, Telefone = t.Telefone,
            ConsentimentoAtendimento = t.ConsentimentoAtendimento,
            ConsentimentoLembretes = t.ConsentimentoLembretes,
            ConsentimentoCompartilhamento = t.ConsentimentoCompartilhamento,
            DataConsentimento = t.DataConsentimento, Ativo = t.Ativo,
            TierFidelidade = t.TierFidelidade, SaldoPontos = t.SaldoPontos,
            SaldoCreditosVetly = t.SaldoCreditosVetly,
            NoShowsAtivos = t.NoShowsAtivos(agora),
            BloqueadoDescontosAte = t.BloqueadoDescontosAte
        };
    }
}
