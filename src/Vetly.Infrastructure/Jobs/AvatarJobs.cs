using Microsoft.Extensions.Logging;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Grava a condição do dia de todos os avatares no histórico (RN-114).
///
/// <para>
/// Sem esta rotina, o histórico só teria os dias em que o Responsável abriu o app —
/// e o gráfico de evolução ficaria cheio de buracos justamente nos períodos de
/// abandono, que são os que mais importam mostrar. O <c>GET /avatar</c> também grava
/// (upsert do mesmo dia), então o registro de um dia reflete a última leitura.
/// </para>
/// <para>
/// Um animal com falha não interrompe os outros: o erro vai para o log e a varredura
/// segue. Um Mongo fora do ar faz a rotina inteira falhar cedo, e o worker já isola a
/// falha das demais rotinas.
/// </para>
/// </summary>
public class RegistrarCondicaoDosAvatares : IRotinaPeriodica
{
    private readonly IAnimalRepository _animais;
    private readonly CalculadoraDoAvatar _calculadora;
    private readonly IHistoricoDoAvatarRepository _historico;
    private readonly ILogger<RegistrarCondicaoDosAvatares> _logger;

    public RegistrarCondicaoDosAvatares(
        IAnimalRepository animais,
        CalculadoraDoAvatar calculadora,
        IHistoricoDoAvatarRepository historico,
        ILogger<RegistrarCondicaoDosAvatares> logger)
    {
        _animais = animais;
        _calculadora = calculadora;
        _historico = historico;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Nome => "RegistrarCondicaoDosAvatares";

    /// <inheritdoc/>
    public TimeSpan Intervalo => TimeSpan.FromHours(24);

    /// <inheritdoc/>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var registrados = 0;

        foreach (var animal in await _animais.ObterAtivosAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var avatar = await _calculadora.CalcularAsync(animal, agora);
                await _historico.RegistrarAsync(RegistroDiarioDoAvatarDto.De(avatar), cancellationToken);
                registrados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Falha ao registrar o avatar do animal {AnimalId}.", animal.Id);
            }
        }

        if (registrados > 0)
            _logger.LogInformation("Condicao do dia registrada para {Quantidade} avatar(es).", registrados);

        return registrados;
    }
}
