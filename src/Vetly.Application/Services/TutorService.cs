using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Tutor;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>Servico de tutores. Gerencia cadastro e consentimentos LGPD.</summary>
public class TutorService : ITutorService
{
    private readonly ITutorRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IUsuarioAtual _usuario;

    public TutorService(ITutorRepository repo, IAnimalRepository animalRepo, IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _usuario = usuario;
    }

    /// <summary>
    /// Recusa acesso aos dados de outro Responsável (RN-105/RN-106). O Admin passa;
    /// o Tutor só alcança o próprio cadastro.
    /// </summary>
    private void GarantirPosse(Guid tutorId)
    {
        if (_usuario.EhAdmin || _usuario.TutorId == tutorId)
            return;

        throw new AcessoNegadoException("RN-106", "Este cadastro nao pertence ao seu escopo de acesso.");
    }

    public async Task<IEnumerable<TutorDto>> ObterTodosAsync()
    {
        var tutores = await _repo.ObterAtivosAsync();
        return tutores.Select(MapearParaDto);
    }

    public async Task<TutorDto> ObterPorIdAsync(Guid id)
    {
        GarantirPosse(id);

        var tutor = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Tutor", id);
        return MapearParaDto(tutor);
    }

    public async Task<IEnumerable<AnimalDto>> ObterAnimaisAsync(Guid tutorId)
    {
        GarantirPosse(tutorId);

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
            throw new BusinessRuleException("TUTOR-001", $"E-mail '{dto.Email}' ja esta cadastrado.");

        var tutor = new Tutor(dto.Nome, dto.Email, dto.Telefone);
        await _repo.AdicionarAsync(tutor);
        await _repo.SalvarAsync();
        return MapearParaDto(tutor);
    }

    public async Task AtualizarAsync(Guid id, CriarTutorDto dto)
    {
        GarantirPosse(id);

        var tutor = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Tutor", id);
        tutor.AtualizarDados(dto.Nome, dto.Email, dto.Telefone);
        _repo.Atualizar(tutor);
        await _repo.SalvarAsync();
    }

    public async Task DesativarAsync(Guid id)
    {
        GarantirPosse(id);

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
