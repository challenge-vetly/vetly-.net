using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Avança a régua de lembretes nos marcos de 7, 3 e 1 dia antes do evento
/// (RN-094/RN-095).
///
/// Antes desta rotina, a régua nascia e parava: o lembrete era criado quando a
/// obrigação entrava na janela de aviso e ninguém mais o tocava. Três tentativas sem
/// resposta acionam o alerta à clínica — só que as tentativas nunca aconteciam, e o
/// alerta que existe para pegar o animal que sumiu nunca disparava.
///
/// Os marcos são decrescentes de propósito. Um aviso a sete dias é planejamento; um a
/// três, lembrete; um a um dia, urgência. Espalhar três avisos iguais na mesma semana
/// seria a mesma frequência com menos utilidade, e o Responsável desligaria a
/// notificação inteira.
/// </summary>
public class AgendarTentativasDaRegua : IRotinaPeriodica
{
    private readonly ILembreteRepository _lembretes;
    private readonly INotificacaoRepository _notificacoes;
    private readonly ILogger<AgendarTentativasDaRegua> _logger;

    /// <summary>
    /// Dias que faltam para o evento em cada degrau da régua (RN-094).
    /// </summary>
    private static readonly int[] Marcos = [7, 3, 1];

    public AgendarTentativasDaRegua(
        ILembreteRepository lembretes,
        INotificacaoRepository notificacoes,
        ILogger<AgendarTentativasDaRegua> logger)
    {
        _lembretes = lembretes;
        _notificacoes = notificacoes;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Nome => "AgendarTentativasDaRegua";

    /// <inheritdoc/>
    /// <remarks>
    /// Uma vez por dia porque os marcos são medidos em dias. Rodar de hora em hora não
    /// adiantaria a próxima tentativa e só multiplicaria leituras.
    /// </remarks>
    public TimeSpan Intervalo => TimeSpan.FromHours(24);

    /// <inheritdoc/>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;

        // Só o que já entrou no primeiro degrau interessa: régua com evento a trinta
        // dias não tem tentativa devida nenhuma.
        var limite = agora.AddDays(Marcos[0]);

        var ativos = (await _lembretes.ObterAtivosAteAsync(limite)).ToList();

        if (ativos.Count == 0)
            return 0;

        var tentativas = 0;

        foreach (var lembrete in ativos)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var devidas = TentativasDevidas(lembrete.DataEvento, agora);

            // Uma tentativa por execução, mesmo com vários marcos vencidos de uma vez:
            // uma régua que ficou parada não vira três notificações no mesmo minuto.
            if (lembrete.TentativasRealizadas >= devidas)
                continue;

            lembrete.RegistrarTentativa();
            _lembretes.Atualizar(lembrete);

            await _notificacoes.AdicionarAsync(new Notificacao(
                lembrete.TutorId,
                TipoNotificacao.ObrigacaoVencendo,
                TituloDoDegrau(lembrete, agora),
                CorpoDoDegrau(lembrete, agora),
                agendadaPara: agora,
                animalId: lembrete.AnimalId,
                destino: $"/animais/{lembrete.AnimalId}/obrigacoes"));

            tentativas++;

            if (lembrete.AlertaEnviadoClinica)
            {
                _logger.LogInformation(
                    "Regua do lembrete {LembreteId} escalou para a clinica apos {Tentativas} tentativas.",
                    lembrete.Id, lembrete.TentativasRealizadas);
            }
        }

        if (tentativas > 0)
        {
            await _lembretes.SalvarAsync();
            await _notificacoes.SalvarAsync();

            _logger.LogInformation("Regua avancou em {Tentativas} lembrete(s).", tentativas);
        }

        return tentativas;
    }

    /// <summary>
    /// Quantos degraus da régua já venceram. Evento no passado conta todos: a régua não
    /// para de existir porque a data passou — é justamente aí que ela mais importa.
    /// </summary>
    private static int TentativasDevidas(DateTime dataEvento, DateTime agora)
    {
        var diasRestantes = (dataEvento - agora).TotalDays;

        return Marcos.Count(marco => diasRestantes <= marco);
    }

    private static string TituloDoDegrau(LembreteAgendado lembrete, DateTime agora) =>
        lembrete.DataEvento < agora
            ? $"{Assunto(lembrete.Tipo)} em atraso"
            : $"{Assunto(lembrete.Tipo)} chegando";

    private static string CorpoDoDegrau(LembreteAgendado lembrete, DateTime agora)
    {
        var dias = (int)Math.Ceiling((lembrete.DataEvento - agora).TotalDays);

        return dias switch
        {
            < 0 => $"{Assunto(lembrete.Tipo)} venceu em {lembrete.DataEvento:dd/MM/yyyy}. " +
                   "Toque para reagendar.",
            0 => $"{Assunto(lembrete.Tipo)} e hoje. Toque para confirmar.",
            1 => $"{Assunto(lembrete.Tipo)} e amanha. Toque para confirmar.",
            _ => $"{Assunto(lembrete.Tipo)} em {dias} dias. Toque para confirmar."
        };
    }

    /// <summary>Como o assunto aparece para quem cuida do animal, e não no jargão do enum.</summary>
    private static string Assunto(TipoLembrete tipo) => tipo switch
    {
        TipoLembrete.Vacina => "Vacina",
        TipoLembrete.Vermifugo => "Vermifugo",
        TipoLembrete.Retorno => "Retorno",
        TipoLembrete.Medicacao => "Medicacao",
        TipoLembrete.CheckUp => "Check-up",
        _ => "Cuidado"
    };
}
