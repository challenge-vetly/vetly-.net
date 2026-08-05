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
    private readonly IRegistroOcultadoRepository _registroOcultadoRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AnimalService(
        IAnimalRepository repo,
        IRegistroOcultadoRepository registroOcultadoRepo,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repo = repo;
        _registroOcultadoRepo = registroOcultadoRepo;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

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

    /// <summary>
    /// Retorna o histórico longitudinal de prontuários. Quando o chamador é um
    /// veterinário, os prontuários ocultados pelo Responsável (RN-088) são
    /// filtrados da lista — o Responsável sempre vê tudo.
    /// </summary>
    public async Task<IEnumerable<ProntuarioDto>> ObterHistoricoAsync(Guid animalId)
    {
        var prontuarios = await _repo.ObterHistoricoLongitudinalAsync(animalId);

        var ocultadosIds = _currentUser.Role == "Veterinario"
            ? (await _registroOcultadoRepo.ObterPorAnimalAsync(animalId)).Select(r => r.ProntuarioId).ToHashSet()
            : [];

        return prontuarios
            .Where(p => !ocultadosIds.Contains(p.Id))
            .Select(p => new ProntuarioDto
            {
                Id = p.Id, ConsultaId = p.ConsultaId, AnimalId = p.AnimalId,
                DadosClinicos = p.DadosClinicos, VersaoOriginalId = p.VersaoOriginalId,
                DataCorrecao = p.DataCorrecao, JustificativaCorrecao = p.JustificativaCorrecao,
                CrmvSolicitanteCorrecao = p.CrmvSolicitanteCorrecao,
                DataCriacao = p.DataCriacao, ExigeJustificativa = p.ExigeJustificativa(),
                AlertaSeguranca = p.AlertaSeguranca, Ocultado = ocultadosIds.Contains(p.Id)
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
        var animal = new Animal(
            dto.Nome, dto.Especie, dto.Raca, dto.Sexo, dto.DataNascimento, dto.ResponsavelId,
            dto.Castrado, dto.PesoKg, dto.FotoUrl);
        animal.AtualizarDadosClinicos(dto.CondicoesPreExistentes, dto.Alergias, dto.CarteiraVacinacao, dto.MedicacoesEmUso);

        await _repo.AdicionarAsync(animal);
        await _repo.SalvarAsync();
        return MapearParaDto(animal);
    }

    public async Task AtualizarAsync(Guid id, CriarAnimalDto dto)
    {
        var animal = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Animal", id);
        animal.AtualizarDados(dto.Nome, dto.Especie, dto.Raca, dto.Sexo, dto.DataNascimento, dto.Castrado, dto.FotoUrl);
        animal.AtualizarDadosClinicos(dto.CondicoesPreExistentes, dto.Alergias, dto.CarteiraVacinacao, dto.MedicacoesEmUso);
        if (dto.PesoKg is { } pesoKg)
            animal.AtualizarPeso(pesoKg);

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

    public async Task<AnimalDto> AtualizarPesoAsync(Guid id, decimal pesoKg)
    {
        var animal = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Animal", id);
        animal.AtualizarPeso(pesoKg);
        _repo.Atualizar(animal);
        await _repo.SalvarAsync();
        return MapearParaDto(animal);
    }

    public async Task OcultarRegistroAsync(Guid animalId, Guid prontuarioId)
    {
        var animal = await _repo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);
        var prontuario = await _repo.ObterProntuarioPorIdAsync(prontuarioId)
            ?? throw new NotFoundException("Prontuario", prontuarioId);

        var registro = animal.OcultarRegistro(prontuarioId, prontuario.AlertaSeguranca, _timeProvider.GetUtcNow().UtcDateTime);

        await _registroOcultadoRepo.AdicionarAsync(registro);
        await _registroOcultadoRepo.SalvarAsync();
    }

    public async Task ReexibirRegistroAsync(Guid animalId, Guid prontuarioId)
    {
        var registro = await _registroOcultadoRepo.ObterAsync(animalId, prontuarioId)
            ?? throw new NotFoundException(
                $"Não há registro ocultado para o prontuário '{prontuarioId}' deste animal.");

        _registroOcultadoRepo.Remover(registro);
        await _registroOcultadoRepo.SalvarAsync();
    }

    /// <summary>
    /// Mapeamento público para reuso por outros serviços que projetam Animal em contextos
    /// próprios (ex: ResponsavelService.ObterAnimaisAsync, ConsultaService.ObterBriefingAsync)
    /// — evita duplicar a lista de campos do Animal em múltiplos lugares.
    /// </summary>
    public static AnimalDto MapearParaDto(Animal a) => new()
    {
        Id = a.Id, Nome = a.Nome, Especie = a.Especie, Raca = a.Raca, Sexo = a.Sexo,
        DataNascimento = a.DataNascimento, IdadeEmAnos = a.IdadeEmAnos(),
        ResponsavelId = a.ResponsavelId, AlertasAtivos = a.AlertasAtivos, Ativo = a.Ativo,
        PesoKg = a.PesoKg, Castrado = a.Castrado,
        CondicoesPreExistentes = a.CondicoesPreExistentes, Alergias = a.Alergias,
        CarteiraVacinacao = a.CarteiraVacinacao, MedicacoesEmUso = a.MedicacoesEmUso,
        FotoUrl = a.FotoUrl
    };
}
