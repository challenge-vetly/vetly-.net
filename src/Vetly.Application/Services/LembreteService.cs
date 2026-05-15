using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de lembretes agendados.
/// Controla regua de contato e alerta clinica apos 3 tentativas sem resposta (RN-029/030).
/// </summary>
public class LembreteService : ILembreteService
{
    private readonly ILembreteRepository _repo;

    public LembreteService(ILembreteRepository repo) => _repo = repo;

    public async Task<LembreteAgendado> AgendarLembreteAsync(Guid animalId, Guid tutorId, TipoLembrete tipo, DateTime dataEvento)
    {
        var lembrete = new LembreteAgendado(animalId, tutorId, tipo, dataEvento);
        await _repo.AdicionarAsync(lembrete);
        await _repo.SalvarAsync();
        return lembrete;
    }

    /// <summary>
    /// Registra tentativa de contato. Aciona alerta para clinica apos 3 tentativas (RN-030).
    /// Se o tutor ja respondeu, a regua esta encerrada e nao registra nova tentativa.
    /// </summary>
    public async Task<LembreteAgendado> ProcessarTentativaAsync(Guid lembreteId)
    {
        var lembrete = await _repo.ObterPorIdAsync(lembreteId)
            ?? throw new NotFoundException("LembreteAgendado", lembreteId);

        if (lembrete.TutorRespondeu)
            throw new BusinessRuleException("LEMBRETE-001", "Regua encerrada: tutor ja respondeu ao lembrete.");

        lembrete.RegistrarTentativa();
        _repo.Atualizar(lembrete);
        await _repo.SalvarAsync();
        return lembrete;
    }

    public async Task<LembreteAgendado> RegistrarRespostaAsync(Guid lembreteId)
    {
        var lembrete = await _repo.ObterPorIdAsync(lembreteId)
            ?? throw new NotFoundException("LembreteAgendado", lembreteId);

        lembrete.RegistrarResposta();
        _repo.Atualizar(lembrete);
        await _repo.SalvarAsync();
        return lembrete;
    }
}
