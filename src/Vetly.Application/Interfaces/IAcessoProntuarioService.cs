using Vetly.Application.DTOs.Prontuario;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Decide e registra o acesso de veterinários ao prontuário de um animal, seguindo o
/// modelo de colmeia por evento clínico (RN-010, RN-083..088).
/// </summary>
public interface IAcessoProntuarioService
{
    /// <summary>True se o veterinário pode acessar algo do animal — via colmeia ou por já tê-lo atendido.</summary>
    Task<bool> PodeAcessarAsync(Guid veterinarioId, Guid animalId, DateTime agora);

    /// <summary>True se o veterinário tem concessão de colmeia ativa (acesso ao histórico completo — RN-083).</summary>
    Task<bool> TemAcessoCompletoAsync(Guid veterinarioId, Guid animalId, DateTime agora);

    /// <summary>Registra um acesso efetivo no log de auditoria (RN-086).</summary>
    Task RegistrarAcessoAsync(Guid veterinarioId, Guid animalId, string contexto, DateTime agora);

    /// <summary>
    /// Concede acesso de colmeia ao confirmar uma consulta, se o Responsável tem
    /// consentimento de compartilhamento na rede ativo (RN-083). Não faz nada caso
    /// contrário — o vet permanece no acesso restrito clássico (RN-010).
    /// </summary>
    Task ConcederAcessoPorConsultaAsync(
        Guid consultaId, Guid veterinarioId, Guid animalId, Guid responsavelId, DateTime dataConsulta, DateTime agora);

    /// <summary>Retorna as concessões ativas de um veterinário (uso administrativo/depuração).</summary>
    Task<IEnumerable<ConcessaoAcessoProntuarioDto>> ObterConcessoesAtivasAsync(Guid veterinarioId, DateTime agora);

    /// <summary>Retorna o log completo de acessos ao prontuário de um animal — visível ao Responsável (RN-086).</summary>
    Task<IEnumerable<LogAcessoProntuarioDto>> ObterLogAcessosAsync(Guid animalId);
}
