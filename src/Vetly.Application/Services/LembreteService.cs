using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de lembretes agendados.
/// Controla regua de contato e alerta clinica apos 3 tentativas sem resposta (RN-094/RN-095).
/// </summary>
public class LembreteService : ILembreteService
{
    private readonly ILembreteRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IUsuarioAtual _usuario;

    public LembreteService(
        ILembreteRepository repo, IAnimalRepository animalRepo, IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _usuario = usuario;
    }

    /// <summary>
    /// O lembrete e do animal que o veterinario atende (RN-105). Sem essa guarda,
    /// qualquer profissional agendaria regua de contato sobre o pet de outro — e a
    /// regua termina em push no telefone do Responsavel.
    /// </summary>
    private async Task GarantirEscopoDoAnimalAsync(Guid animalId)
    {
        if (_usuario.EhAdmin)
            return;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId
            && await _animalRepo.VeterinarioAtendeAnimalAsync(vetId, animalId))
        {
            return;
        }

        throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");
    }

    /// <summary>
    /// A resposta e do Responsavel: e ele quem encerra a regua dizendo que recebeu o
    /// recado (RN-094). O veterinario nao responde no lugar dele.
    /// </summary>
    private void GarantirQueEODono(Guid tutorId)
    {
        if (_usuario.EhAdmin || _usuario.TutorId == tutorId)
            return;

        throw new AcessoNegadoException("RN-106", "Este lembrete nao pertence ao seu escopo de acesso.");
    }

    public async Task<LembreteAgendado> AgendarLembreteAsync(Guid animalId, Guid tutorId, TipoLembrete tipo, DateTime dataEvento)
    {
        await GarantirEscopoDoAnimalAsync(animalId);

        var lembrete = new LembreteAgendado(animalId, tutorId, tipo, dataEvento);
        await _repo.AdicionarAsync(lembrete);
        await _repo.SalvarAsync();
        return lembrete;
    }

    /// <summary>
    /// Registra tentativa de contato. Aciona alerta para clinica apos 3 tentativas (RN-095).
    /// Se o tutor ja respondeu, a regua esta encerrada e nao registra nova tentativa.
    /// </summary>
    public async Task<LembreteAgendado> ProcessarTentativaAsync(Guid lembreteId)
    {
        var lembrete = await _repo.ObterPorIdAsync(lembreteId)
            ?? throw new NotFoundException("LembreteAgendado", lembreteId);

        await GarantirEscopoDoAnimalAsync(lembrete.AnimalId);

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

        GarantirQueEODono(lembrete.TutorId);

        lembrete.RegistrarResposta();
        _repo.Atualizar(lembrete);
        await _repo.SalvarAsync();
        return lembrete;
    }
}
