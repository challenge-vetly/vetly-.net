using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Devolve à disponibilidade os horários cujo lock de checkout venceu, e expira as
/// consultas que ficaram penduradas neles (RN-035).
///
/// A leitura de disponibilidade já trata lock vencido como livre, então esta rotina
/// não é o que impede overbooking. O que ela acrescenta é o <b>estado no banco</b>
/// coerente com a realidade — e, principalmente, o gatilho da lista de espera: quem
/// está na fila só é avisado quando alguém percebe que o horário voltou (RN-037).
/// </summary>
public class ExpirarLocksDeCheckout : IRotinaPeriodica
{
    private readonly VetlyDbContext _context;
    private readonly IFilaDeJobs _fila;
    private readonly ILogger<ExpirarLocksDeCheckout> _logger;

    public ExpirarLocksDeCheckout(
        VetlyDbContext context, IFilaDeJobs fila, ILogger<ExpirarLocksDeCheckout> logger)
    {
        _context = context;
        _fila = fila;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Nome => "ExpirarLocksDeCheckout";

    /// <inheritdoc/>
    public TimeSpan Intervalo => TimeSpan.FromMinutes(1);

    /// <inheritdoc/>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;

        var vencidos = await _context.Slots
            .Where(s => s.Estado == EstadoSlot.EmCheckout && s.LockAte != null && s.LockAte < agora)
            .ToListAsync(cancellationToken);

        if (vencidos.Count == 0)
            return 0;

        foreach (var slot in vencidos)
        {
            var consultaId = slot.LockConsultaId;

            slot.Liberar();

            if (consultaId is { } id)
            {
                var consulta = await _context.Consultas
                    .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

                // So expira quem continua em checkout: se o pagamento entrou no meio do
                // caminho, a consulta ja foi confirmada e nao pode ser desfeita aqui.
                if (consulta is not null && consulta.Status == StatusConsulta.EmCheckout)
                    consulta.Expirar();
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Toda entrada em "livre" avisa a lista de espera (RN-037)
        foreach (var slot in vencidos)
            await _fila.EnfileirarAsync(TipoJob.PromoverListaEspera, slot.Id.ToString());

        _logger.LogInformation("{Quantidade} lock(s) de checkout expiraram e voltaram a ficar livres.", vencidos.Count);

        return vencidos.Count;
    }
}

/// <summary>
/// Apaga os registros de idempotência vencidos (§2.5, retenção de 24h em §6.5).
///
/// Não muda comportamento — a vigência já é conferida na leitura —, é higiene de
/// tabela: sem isso ela cresce para sempre.
/// </summary>
public class LimparIdempotenciaVencida : IRotinaPeriodica
{
    private readonly VetlyDbContext _context;
    private readonly ILogger<LimparIdempotenciaVencida> _logger;

    public LimparIdempotenciaVencida(VetlyDbContext context, ILogger<LimparIdempotenciaVencida> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Nome => "LimparIdempotenciaVencida";

    /// <inheritdoc/>
    public TimeSpan Intervalo => TimeSpan.FromHours(1);

    /// <summary>Teto de remoções por ciclo, para a faxina não segurar o worker.</summary>
    private const int LotePorCiclo = 500;

    /// <inheritdoc/>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;

        var vencidos = await _context.RegistrosDeIdempotencia
            .Where(r => r.ExpiraEm < agora)
            .Take(LotePorCiclo)
            .ToListAsync(cancellationToken);

        if (vencidos.Count == 0)
            return 0;

        _context.RegistrosDeIdempotencia.RemoveRange(vencidos);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("{Quantidade} registro(s) de idempotencia vencidos removidos.", vencidos.Count);

        return vencidos.Count;
    }
}
