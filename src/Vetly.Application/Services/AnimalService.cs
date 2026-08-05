using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Exame;
using Vetly.Application.DTOs.Prontuario;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>Servico de animais. Gerencia cadastro, historico longitudinal e exames.</summary>
public class AnimalService : IAnimalService
{
    private readonly IAnimalRepository _repo;

    public AnimalService(IAnimalRepository repo) => _repo = repo;

    public async Task<IEnumerable<AnimalDto>> ObterTodosAsync()
    {
        var animais = await _repo.ObterAtivosAsync();
        return animais.Select(MapearParaDto);
    }

    public async Task<AnimalDto> ObterPorIdAsync(Guid id)
    {
        var animal = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Animal", id);
        return MapearParaDto(animal);
    }

    public async Task<IEnumerable<ProntuarioDto>> ObterHistoricoAsync(Guid animalId)
    {
        var prontuarios = await _repo.ObterHistoricoLongitudinalAsync(animalId);
        return prontuarios.Select(p => new ProntuarioDto
        {
            Id = p.Id, ConsultaId = p.ConsultaId, AnimalId = p.AnimalId,
            DadosClinicos = p.DadosClinicos, VersaoOriginalId = p.VersaoOriginalId,
            DataCorrecao = p.DataCorrecao, JustificativaCorrecao = p.JustificativaCorrecao,
            CrmvSolicitanteCorrecao = p.CrmvSolicitanteCorrecao,
            DataCriacao = p.DataCriacao, ExigeJustificativa = p.ExigeJustificativa()
        });
    }

    public async Task<IEnumerable<ExameDto>> ObterExamesAsync(Guid animalId)
    {
        var exames = await _repo.ObterExamesAsync(animalId);
        return exames.Select(e => new ExameDto
        {
            Id = e.Id, AnimalId = e.AnimalId, VeterinarioId = e.VeterinarioId,
            TipoSolicitacao = e.TipoSolicitacao, Resultado = e.Resultado,
            LiberadoAoResponsavel = e.LiberadoAoResponsavel,
            DataSolicitacao = e.DataSolicitacao, DataResultado = e.DataResultado
        });
    }

    public async Task<AnimalDto> CriarAsync(CriarAnimalDto dto)
    {
        var animal = new Animal(dto.Nome, dto.Especie, dto.Raca, dto.DataNascimento, dto.ResponsavelId);
        await _repo.AdicionarAsync(animal);
        await _repo.SalvarAsync();
        return MapearParaDto(animal);
    }

    public async Task AtualizarAsync(Guid id, CriarAnimalDto dto)
    {
        var animal = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Animal", id);
        animal.AtualizarDados(dto.Nome, dto.Especie, dto.Raca, dto.DataNascimento);
        _repo.Atualizar(animal);
        await _repo.SalvarAsync();
    }

    public async Task DesativarAsync(Guid id)
    {
        var animal = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Animal", id);
        animal.Desativar();
        _repo.Atualizar(animal);
        await _repo.SalvarAsync();
    }

    private static AnimalDto MapearParaDto(Animal a) => new()
    {
        Id = a.Id, Nome = a.Nome, Especie = a.Especie, Raca = a.Raca,
        DataNascimento = a.DataNascimento, IdadeEmAnos = a.IdadeEmAnos(),
        ResponsavelId = a.ResponsavelId, AlertasAtivos = a.AlertasAtivos, Ativo = a.Ativo
    };
}
