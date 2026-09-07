using Microsoft.Extensions.Logging;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Entrega as notificações cuja hora chegou (RN-092).
///
/// Roda fora da requisição porque push é rede: o Responsável não pode esperar o APNs
/// responder para que a consulta seja confirmada, e o provedor fora do ar não pode
/// derrubar nada.
/// </summary>
public class EnviarNotificacoesPendentes : IRotinaPeriodica
{
    private readonly INotificacaoService _notificacoes;
    private readonly ILogger<EnviarNotificacoesPendentes> _logger;

    /// <summary>Quantas notificações cada volta processa. Lote pequeno mantém o ciclo curto.</summary>
    private const int TamanhoDoLote = 50;

    public EnviarNotificacoesPendentes(
        INotificacaoService notificacoes, ILogger<EnviarNotificacoesPendentes> logger)
    {
        _notificacoes = notificacoes;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Nome => "EnviarNotificacoesPendentes";

    /// <inheritdoc/>
    public TimeSpan Intervalo => TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var pendentes = (await _notificacoes.ObterPendentesParaEnvioAsync(TamanhoDoLote)).ToList();

        var entregues = 0;

        foreach (var id in pendentes)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Falha de uma notificação não pode parar o lote: cada uma já registra a
            // própria tentativa, e a próxima volta retenta.
            try
            {
                if (await _notificacoes.EntregarAsync(id))
                    entregues++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao entregar a notificacao {NotificacaoId}.", id);
            }
        }

        if (entregues > 0)
            _logger.LogInformation("Entregues {Total} notificacao(oes) por push.", entregues);

        return entregues;
    }
}

/// <summary>
/// Transforma obrigações vencendo em aviso ao Responsável (RN-045/RN-094).
///
/// É a régua de lembretes: sem ela, o board de obrigações é uma tela que só quem
/// abre o app descobre — e quem já esqueceu da vacina é exatamente quem não abre.
///
/// Roda uma vez por dia porque obrigação vence em escala de dias. Avisar de hora em
/// hora sobre a mesma vacina seria transformar cuidado em incômodo, e o Responsável
/// desligaria a notificação inteira.
/// </summary>
public class AvisarObrigacoesVencendo : IRotinaPeriodica
{
    private readonly IObrigacaoRepository _obrigacoes;
    private readonly INotificacaoRepository _notificacoes;
    private readonly ILembreteRepository _lembretes;
    private readonly ILogger<AvisarObrigacoesVencendo> _logger;

    /// <summary>
    /// Intervalo mínimo entre dois avisos sobre o mesmo animal. Sete dias é o que
    /// separa lembrar de perseguir.
    /// </summary>
    private static readonly TimeSpan IntervaloEntreAvisos = TimeSpan.FromDays(7);

    public AvisarObrigacoesVencendo(
        IObrigacaoRepository obrigacoes,
        INotificacaoRepository notificacoes,
        ILembreteRepository lembretes,
        ILogger<AvisarObrigacoesVencendo> logger)
    {
        _obrigacoes = obrigacoes;
        _notificacoes = notificacoes;
        _lembretes = lembretes;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Nome => "AvisarObrigacoesVencendo";

    /// <inheritdoc/>
    public TimeSpan Intervalo => TimeSpan.FromHours(24);

    /// <inheritdoc/>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var limite = agora.Add(ObrigacaoPet.JanelaDeAviso);

        var vencendo = (await _obrigacoes.ObterVencendoAteAsync(limite)).ToList();

        if (vencendo.Count == 0)
            return 0;

        var criadas = 0;

        // Uma notificação por animal, não por obrigação: três vacinas vencendo na
        // mesma semana são um aviso, não três.
        foreach (var doAnimal in vencendo.GroupBy(o => o.AnimalId))
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var jaAvisado = await _notificacoes.ObterDoAnimalPorTipoDesdeAsync(
                doAnimal.Key, TipoNotificacao.ObrigacaoVencendo, agora.Subtract(IntervaloEntreAvisos));

            if (jaAvisado is not null)
                continue;

            var itens = doAnimal.OrderBy(o => o.ProximoVencimento).ToList();
            var vencidas = itens.Count(o => o.SituacaoEm(agora) == SituacaoObrigacao.Vencida);

            await _notificacoes.AdicionarAsync(new Notificacao(
                itens[0].TutorId,
                TipoNotificacao.ObrigacaoVencendo,
                vencidas > 0 ? "Cuidados em atraso" : "Cuidados chegando",
                MontarCorpo(itens, vencidas),
                agendadaPara: agora,
                animalId: doAnimal.Key,
                destino: $"/animais/{doAnimal.Key}/obrigacoes"));

            // O lembrete é o que sustenta a régua: três tentativas sem resposta
            // acionam o alerta à clínica (RN-095).
            await _lembretes.AdicionarAsync(new LembreteAgendado(
                doAnimal.Key, itens[0].TutorId,
                LembreteEquivalente(itens[0].Tipo), itens[0].ProximoVencimento));

            criadas++;
        }

        if (criadas > 0)
        {
            await _notificacoes.SalvarAsync();
            await _lembretes.SalvarAsync();

            _logger.LogInformation("Criados {Total} aviso(s) de obrigacao vencendo.", criadas);
        }

        return criadas;
    }

    /// <summary>
    /// Texto do aviso. Nomeia a obrigação mais urgente em vez de dizer "você tem
    /// pendências": aviso genérico é aviso que não move ninguém.
    /// </summary>
    private static string MontarCorpo(List<ObrigacaoPet> itens, int vencidas)
    {
        var primeira = itens[0];

        var restantes = itens.Count - 1;
        var complemento = restantes > 0 ? $" e mais {restantes} cuidado(s)" : string.Empty;

        return vencidas > 0
            ? $"{primeira.Descricao} esta em atraso{complemento}. Toque para ver o que fazer."
            : $"{primeira.Descricao} vence em breve{complemento}. Toque para agendar.";
    }

    /// <summary>
    /// Assunto da régua correspondente à obrigação que a abriu (§6.3).
    ///
    /// Antes disto toda régua nascia como <c>Vacina</c>, qualquer que fosse a
    /// obrigação. Não era só um rótulo torto: o assunto é o que a régua escreve nas
    /// três tentativas ("Retorno em atraso", "Medicacao e amanha") e o que a clínica
    /// lê no alerta da RN-095. O Responsável recebia três avisos de vacina por um
    /// retorno, e a clínica era acionada sobre uma vacina que ninguém deixou de tomar.
    ///
    /// <see cref="TipoObrigacaoPet.Antiparasitario"/> cai em
    /// <see cref="TipoLembrete.Vermifugo"/> e <see cref="TipoObrigacaoPet.Exame"/> em
    /// <see cref="TipoLembrete.CheckUp"/> porque a régua tem cinco assuntos e a
    /// obrigação tem sete: o mapa aproxima pelo que o Responsável precisa fazer —
    /// aplicar um antiparasitário é a mesma tarefa doméstica do vermífugo, e um exame
    /// de acompanhamento é a mesma ida à clínica do check-up.
    /// </summary>
    private static TipoLembrete LembreteEquivalente(TipoObrigacaoPet tipo) => tipo switch
    {
        TipoObrigacaoPet.Vacina => TipoLembrete.Vacina,
        TipoObrigacaoPet.Vermifugo => TipoLembrete.Vermifugo,
        TipoObrigacaoPet.Antiparasitario => TipoLembrete.Vermifugo,
        TipoObrigacaoPet.Retorno => TipoLembrete.Retorno,
        TipoObrigacaoPet.MedicacaoContinua => TipoLembrete.Medicacao,
        TipoObrigacaoPet.CheckUp => TipoLembrete.CheckUp,
        TipoObrigacaoPet.Exame => TipoLembrete.CheckUp,
        _ => TipoLembrete.CheckUp
    };
}
