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
    private readonly IAcessoProntuarioService _acessoProntuarioService;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;

    public AnimalService(
        IAnimalRepository repo,
        IRegistroOcultadoRepository registroOcultadoRepo,
        IAcessoProntuarioService acessoProntuarioService,
        ICurrentUserService currentUser,
        TimeProvider timeProvider)
    {
        _repo = repo;
        _registroOcultadoRepo = registroOcultadoRepo;
        _acessoProntuarioService = acessoProntuarioService;
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
    /// Retorna o histórico longitudinal de prontuários. Para o Responsável (ou Admin),
    /// sempre tudo. Para um veterinário, aplica a colmeia por evento clínico (RN-010/083):
    /// com concessão ativa, vê tudo (menos os ocultados — RN-088); sem concessão, só o que
    /// ele próprio produziu; sem nenhum vínculo com o animal, 403 (ACESSO-001). Todo acesso
    /// de veterinário é registrado no log de auditoria (RN-086).
    /// </summary>
    public async Task<IEnumerable<ProntuarioDto>> ObterHistoricoAsync(Guid animalId)
    {
        IEnumerable<Prontuario> prontuarios;
        HashSet<Guid> ocultadosIds = [];

        if (_currentUser.Role == "Veterinario")
        {
            var vetId = _currentUser.EntidadeId
                ?? throw new ForbiddenException("ACESSO-001", "Acesso ao prontuario negado.");
            var agora = _timeProvider.GetUtcNow().UtcDateTime;

            if (!await _acessoProntuarioService.PodeAcessarAsync(vetId, animalId, agora))
                throw new ForbiddenException("ACESSO-001", "Acesso ao prontuario negado.");

            var acessoCompleto = await _acessoProntuarioService.TemAcessoCompletoAsync(vetId, animalId, agora);
            prontuarios = acessoCompleto
                ? await _repo.ObterHistoricoLongitudinalAsync(animalId)
                : await _repo.ObterHistoricoLongitudinalPorVeterinarioAsync(animalId, vetId);

            ocultadosIds = (await _registroOcultadoRepo.ObterPorAnimalAsync(animalId))
                .Select(r => r.ProntuarioId).ToHashSet();

            await _acessoProntuarioService.RegistrarAcessoAsync(vetId, animalId, "Listagem de prontuários do animal", agora);
        }
        else
        {
            prontuarios = await _repo.ObterHistoricoLongitudinalAsync(animalId);
        }

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

    /// <summary>Retorna o log completo de acessos ao prontuário do animal — visível ao Responsável (RN-086).</summary>
    public async Task<IEnumerable<LogAcessoProntuarioDto>> ObterLogAcessosAsync(Guid animalId)
    {
        _ = await _repo.ObterPorIdAsync(animalId) ?? throw new NotFoundException("Animal", animalId);
        return await _acessoProntuarioService.ObterLogAcessosAsync(animalId);
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
