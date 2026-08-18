using Vetly.Application.DTOs.Prontuario;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Decide e registra o acesso de veterinários ao prontuário de um animal, seguindo o
/// modelo de colmeia por evento clínico (RN-010, RN-083..088).
/// </summary>
public class AcessoProntuarioService : IAcessoProntuarioService
{
    private readonly IConcessaoAcessoProntuarioRepository _concessaoRepo;
    private readonly ILogAcessoProntuarioRepository _logRepo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IConsentimentoLgpdRepository _consentimentoRepo;

    public AcessoProntuarioService(
        IConcessaoAcessoProntuarioRepository concessaoRepo,
        ILogAcessoProntuarioRepository logRepo,
        IConsultaRepository consultaRepo,
        IConsentimentoLgpdRepository consentimentoRepo)
    {
        _concessaoRepo = concessaoRepo;
        _logRepo = logRepo;
        _consultaRepo = consultaRepo;
        _consentimentoRepo = consentimentoRepo;
    }

    /// <inheritdoc/>
    public async Task<bool> PodeAcessarAsync(Guid veterinarioId, Guid animalId, DateTime agora)
    {
        if (await TemAcessoCompletoAsync(veterinarioId, animalId, agora))
            return true;

        // RN-010: sem colmeia, o acesso restrito clássico exige já ter atendido o animal.
        return await _consultaRepo.ExisteConsultaAsync(veterinarioId, animalId);
    }

    /// <inheritdoc/>
    public async Task<bool> TemAcessoCompletoAsync(Guid veterinarioId, Guid animalId, DateTime agora) =>
        await _concessaoRepo.ObterAtivaAsync(veterinarioId, animalId, agora) is not null;

    /// <inheritdoc/>
    public async Task RegistrarAcessoAsync(Guid veterinarioId, Guid animalId, string contexto, DateTime agora)
    {
        var baseAcesso = await TemAcessoCompletoAsync(veterinarioId, animalId, agora)
            ? BaseAcesso.ConsentimentoRede
            : BaseAcesso.AtendimentoDireto;

        var log = new LogAcessoProntuario(animalId, veterinarioId, agora, contexto, baseAcesso);
        await _logRepo.AdicionarAsync(log);
        await _logRepo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task ConcederAcessoPorConsultaAsync(
        Guid consultaId, Guid veterinarioId, Guid animalId, Guid responsavelId, DateTime dataConsulta, DateTime agora)
    {
        var temConsentimentoRede = await _consentimentoRepo.ObterAtivoAsync(
            responsavelId, FinalidadeConsentimento.CompartilhamentoRede) is not null;
        if (!temConsentimentoRede)
            return; // sem colmeia — o vet segue no acesso restrito clássico (RN-010)

        // Expira ao fim do ciclo (consulta + 24h — RN-085). Retornos vinculados geram sua
        // própria concessão nova ao serem confirmados, renovando o acesso na prática.
        var concessao = new ConcessaoAcessoProntuario(
            animalId, veterinarioId, consultaId, BaseAcesso.ConsentimentoRede, agora, dataConsulta.AddHours(24));

        await _concessaoRepo.AdicionarAsync(concessao);
        await _concessaoRepo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConcessaoAcessoProntuarioDto>> ObterConcessoesAtivasAsync(Guid veterinarioId, DateTime agora)
    {
        var concessoes = await _concessaoRepo.ObterAtivasPorVeterinarioAsync(veterinarioId, agora);
        return concessoes.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LogAcessoProntuarioDto>> ObterLogAcessosAsync(Guid animalId)
    {
        var logs = await _logRepo.ObterPorAnimalAsync(animalId);
        return logs.Select(l => new LogAcessoProntuarioDto
        {
            Id = l.Id, AnimalId = l.AnimalId, VeterinarioId = l.VeterinarioId,
            DataHora = l.DataHora, Contexto = l.Contexto, BaseAcesso = l.BaseAcesso
        });
    }

    private static ConcessaoAcessoProntuarioDto MapearParaDto(ConcessaoAcessoProntuario c) => new()
    {
        Id = c.Id, AnimalId = c.AnimalId, VeterinarioId = c.VeterinarioId, ConsultaId = c.ConsultaId,
        BaseAcesso = c.BaseAcesso, ConcedidoEm = c.ConcedidoEm, ExpiraEm = c.ExpiraEm, Revogada = c.Revogada
    };
}
