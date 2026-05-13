using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Tutor;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Application.Interfaces;

namespace Vetly.Application.Services;

/// <summary>ServiÃ§o de tutores. Gerencia cadastro e consentimentos LGPD.</summary>
public class TutorService : ITutorService
{
    private readonly ITutorRepository _repo;
    private readonly IAnimalRepository _animalRepo;

    public TutorService(ITutorRepository repo, IAnimalRepository animalRepo)
    {
        _repo = repo;
        _animalRepo = animalRepo;
    }

    public async Task<IEnumerable<TutorDto>> ObterTodosAsync()
    {
        var tutores = await _repo.ObterAtivosAsync();
        return tutores.Select(MapearParaDto);
    }

    public async Task<TutorDto> ObterPorIdAsync(Guid id)
    {
        var tutor = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Tutor", id);
        return MapearParaDto(tutor);
    }

    public async Task<IEnumerable<AnimalDto>> ObterAnimaisAsync(Guid tutorId)
    {
        _ = await _repo.ObterPorIdAsync(tutorId)
            ?? throw new NotFoundException("Tutor", tutorId);

        var animais = await _animalRepo.ObterPorTutorAsync(tutorId);
        return animais.Select(a => new AnimalDto
        {
            Id = a.Id, Nome = a.Nome, Especie = a.Especie, Raca = a.Raca,
            DataNascimento = a.DataNascimento, IdadeEmAnos = a.IdadeEmAnos(),
            TutorId = a.TutorId, AlertasAtivos = a.AlertasAtivos, Ativo = a.Ativo
        });
    }

    public async Task<TutorDto> CriarAsync(CriarTutorDto dto)
    {
        var existente = await _repo.ObterPorEmailAsync(dto.Email);
        if (existente is not null)
            throw new BusinessRuleException("TUTOR-001", $"E-mail '{dto.Email}' jÃ¡ estÃ¡ cadastrado.");

        var tutor = new Tutor(dto.Nome, dto.Email, dto.Telefone);
        await _repo.AdicionarAsync(tutor);
        await _repo.SalvarAsync();
        return MapearParaDto(tutor);
    }

    public async Task AtualizarAsync(Guid id, CriarTutorDto dto)
    {
        var tutor = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Tutor", id);
        tutor.AtualizarDados(dto.Nome, dto.Email, dto.Telefone);
        _repo.Atualizar(tutor);
        await _repo.SalvarAsync();
    }

    public async Task DesativarAsync(Guid id)
    {
        var tutor = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Tutor", id);
        tutor.Desativar();
        _repo.Atualizar(tutor);
        await _repo.SalvarAsync();
    }

    private static TutorDto MapearParaDto(Tutor t) => new()
    {
        Id = t.Id, Nome = t.Nome, Email = t.Email, Telefone = t.Telefone,
        ConsentimentoAtendimento = t.ConsentimentoAtendimento,
        ConsentimentoLembretes = t.ConsentimentoLembretes,
        ConsentimentoCompartilhamento = t.ConsentimentoCompartilhamento,
        DataConsentimento = t.DataConsentimento, Ativo = t.Ativo
    };
}
