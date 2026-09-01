using Vetly.Application.DTOs.Notificacao;
using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Notificações ao Responsável e a entrega por push (RN-092/RN-093).
/// </summary>
public interface INotificacaoService
{
    /// <summary>Grava a notificação. O envio acontece depois, fora da requisição.</summary>
    Task<NotificacaoDto> CriarAsync(CriarNotificacaoDto dto);

    /// <summary>
    /// O que o Responsável escolheu receber (RN-093). O escopo vem do token.
    /// </summary>
    Task<PreferenciasDeNotificacaoDto> ObterPreferenciasAsync();

    /// <summary>
    /// Liga ou desliga as comunicações promocionais (RN-093).
    ///
    /// É a única preferência que existe: os demais avisos são o serviço contratado, e
    /// desligá-los faria o app deixar de avisar sobre a saúde do animal.
    /// </summary>
    Task<PreferenciasDeNotificacaoDto> AtualizarPreferenciasAsync(AtualizarPreferenciasDto dto);

    /// <summary>Caixa de entrada do Responsável, da mais recente à mais antiga.</summary>
    Task<IEnumerable<NotificacaoDto>> ObterCaixaDeEntradaAsync(Guid tutorId, bool apenasNaoLidas);

    /// <summary>Registra que o Responsável abriu a notificação no app.</summary>
    Task<NotificacaoDto> MarcarComoLidaAsync(Guid notificacaoId);

    /// <summary>
    /// Tenta entregar por push a todos os dispositivos ativos do Responsável.
    /// Devolve se ao menos um aceitou.
    /// </summary>
    Task<bool> EntregarAsync(Guid notificacaoId);

    /// <summary>Ids das notificações que já podem ser enviadas. É o que a rotina varre.</summary>
    Task<IEnumerable<Guid>> ObterPendentesParaEnvioAsync(int limite);
}

/// <summary>Repositório das notificações (RN-092).</summary>
public interface INotificacaoRepository
{
    Task<Notificacao?> ObterPorIdAsync(Guid id);

    /// <summary>Caixa de entrada de um Responsável, da mais recente à mais antiga.</summary>
    Task<IEnumerable<Notificacao>> ObterDoTutorAsync(Guid tutorId, bool apenasNaoLidas);

    /// <summary>Notificações cuja hora de envio chegou e que ainda não foram entregues.</summary>
    Task<IEnumerable<Notificacao>> ObterPendentesAsync(DateTime agora, int limite);

    /// <summary>
    /// Notificação já criada para o mesmo motivo. É como se evita repetir o mesmo
    /// aviso a cada volta da rotina.
    /// </summary>
    Task<Notificacao?> ObterDoAnimalPorTipoDesdeAsync(Guid animalId, Domain.Enums.TipoNotificacao tipo, DateTime desde);

    Task AdicionarAsync(Notificacao notificacao);
    void Atualizar(Notificacao notificacao);

    Task<int> SalvarAsync();
}
