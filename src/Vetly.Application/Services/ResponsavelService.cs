using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Responsavel;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>Servico de responsaveis. Gerencia cadastro e consentimentos LGPD.</summary>
public class ResponsavelService : IResponsavelService
{
    private readonly IResponsavelRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IConsentimentoLgpdRepository _consentimentoRepo;
    private readonly TimeProvider _timeProvider;

    public ResponsavelService(
        IResponsavelRepository repo,
        IAnimalRepository animalRepo,
        IConsentimentoLgpdRepository consentimentoRepo,
        TimeProvider timeProvider)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _consentimentoRepo = consentimentoRepo;
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
        return animais.Select(AnimalService.MapearParaDto);
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

    public async Task<IEnumerable<ConsentimentoLgpdDto>> ListarConsentimentosAsync(Guid responsavelId)
    {
        _ = await _repo.ObterPorIdAsync(responsavelId)
            ?? throw new NotFoundException("Responsavel", responsavelId);

        var consentimentos = await _consentimentoRepo.ObterPorResponsavelAsync(responsavelId);
        return consentimentos.Select(MapearConsentimentoParaDto);
    }

    /// <summary>
    /// Concede um novo consentimento (RN-041/042/043). Cada chamada cria um registro novo —
    /// mesmo que já exista um consentimento ativo para a mesma finalidade — preservando o
    /// histórico completo de concessões e revogações (RN-044).
    /// </summary>
    public async Task<ConsentimentoLgpdDto> ConcederConsentimentoAsync(Guid responsavelId, ConcederConsentimentoDto dto)
    {
        _ = await _repo.ObterPorIdAsync(responsavelId)
            ?? throw new NotFoundException("Responsavel", responsavelId);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var consentimento = new ConsentimentoLgpd(responsavelId, dto.Finalidade, agora);

        await _consentimentoRepo.AdicionarAsync(consentimento);
        await _consentimentoRepo.SalvarAsync();

        return MapearConsentimentoParaDto(consentimento);
    }

    /// <summary>
    /// Revoga o consentimento ativo mais recente da finalidade informada. O registro
    /// não é apagado — apenas recebe a data de revogação (RN-044, RN-087).
    /// </summary>
    public async Task<ConsentimentoLgpdDto> RevogarConsentimentoAsync(Guid responsavelId, FinalidadeConsentimento finalidade)
    {
        _ = await _repo.ObterPorIdAsync(responsavelId)
            ?? throw new NotFoundException("Responsavel", responsavelId);

        var consentimento = await _consentimentoRepo.ObterAtivoAsync(responsavelId, finalidade)
            ?? throw new NotFoundException(
                $"Não há consentimento ativo para a finalidade '{finalidade}' deste responsável.");

        consentimento.Revogar(_timeProvider.GetUtcNow().UtcDateTime);
        _consentimentoRepo.Atualizar(consentimento);
        await _consentimentoRepo.SalvarAsync();

        return MapearConsentimentoParaDto(consentimento);
    }

    private ResponsavelDto MapearParaDto(Responsavel t)
    {
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        return new ResponsavelDto
        {
            Id = t.Id, Nome = t.Nome, Email = t.Email, Telefone = t.Telefone,
            Ativo = t.Ativo,
            TierFidelidade = t.TierFidelidade, SaldoPontos = t.SaldoPontos,
            SaldoCreditosVetly = t.SaldoCreditosVetly,
            NoShowsAtivos = t.NoShowsAtivos(agora),
            BloqueadoDescontosAte = t.BloqueadoDescontosAte
        };
    }

    private static ConsentimentoLgpdDto MapearConsentimentoParaDto(ConsentimentoLgpd c) => new()
    {
        Id = c.Id, ResponsavelId = c.ResponsavelId, Finalidade = c.Finalidade,
        Ativo = c.Ativo, DataConcessao = c.DataConcessao, DataRevogacao = c.DataRevogacao
    };
}
