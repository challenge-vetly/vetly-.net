using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do servico de lembretes agendados (RN-029/030).</summary>
public interface ILembreteService
{
    /// <summary>Agenda um lembrete para um tutor sobre evento do animal.</summary>
    Task<LembreteAgendado> AgendarLembreteAsync(Guid animalId, Guid tutorId, TipoLembrete tipo, DateTime dataEvento);

    /// <summary>Registra uma tentativa de contato e dispara alerta se >= 3 tentativas (RN-030).</summary>
    Task<LembreteAgendado> ProcessarTentativaAsync(Guid lembreteId);

    /// <summary>Registra a resposta do tutor, encerrando a regua de lembretes (RN-029).</summary>
    Task<LembreteAgendado> RegistrarRespostaAsync(Guid lembreteId);
}
