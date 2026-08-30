using Microsoft.Extensions.Logging;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Implementação simulada do <see cref="ICrmvAdapter"/> (camada C2: API real, dependência
/// externa simulada). Não faz chamada de rede nenhuma.
///
/// O resultado é <b>determinístico pelo último dígito do registro</b>, para que todas as
/// trilhas da RN-107 sejam exercitáveis em teste e demonstração sem depender do conselho:
/// <list type="bullet">
///   <item><description>termina em 7 → <c>Indisponivel</c> (conselho fora do ar)</description></item>
///   <item><description>termina em 8 → <c>Invalido</c></description></item>
///   <item><description>termina em 9 → <c>Suspenso</c></description></item>
///   <item><description>demais → <c>Valido</c></description></item>
/// </list>
/// Antes disso vale uma regra real: se a UF do registro não bate com a UF informada,
/// o resultado é <c>Invalido</c> — um CRMV pertence ao conselho de um estado.
/// </summary>
public class CrmvAdapterSimulado : ICrmvAdapter
{
    private readonly ILogger<CrmvAdapterSimulado> _logger;

    public CrmvAdapterSimulado(ILogger<CrmvAdapterSimulado> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task<ResultadoCrmvDto> ValidarRegistroAsync(string crmv, string uf)
    {
        var partes = (crmv ?? string.Empty).Split('-');
        var numero = partes.Length > 0 ? partes[0] : string.Empty;
        var ufDoRegistro = partes.Length > 1 ? partes[1] : string.Empty;

        var resultado = DecidirResultado(numero, ufDoRegistro, uf);

        _logger.LogInformation(
            "Validacao de CRMV simulada | crmv={Crmv} uf={Uf} resultado={Resultado}",
            crmv, uf, resultado);

        return Task.FromResult(new ResultadoCrmvDto
        {
            Resultado = resultado,
            ConsultadoEm = DateTime.UtcNow,
            Mensagem = MensagemDe(resultado)
        });
    }

    private static ResultadoValidacaoCrmv DecidirResultado(string numero, string ufDoRegistro, string ufInformada)
    {
        // Um CRMV pertence ao conselho de um estado: divergencia de UF invalida o registro
        if (!string.IsNullOrWhiteSpace(ufDoRegistro) &&
            !ufDoRegistro.Equals(ufInformada?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoValidacaoCrmv.Invalido;
        }

        return numero.Length == 0 ? ResultadoValidacaoCrmv.Invalido : numero[^1] switch
        {
            '7' => ResultadoValidacaoCrmv.Indisponivel,
            '8' => ResultadoValidacaoCrmv.Invalido,
            '9' => ResultadoValidacaoCrmv.Suspenso,
            _ => ResultadoValidacaoCrmv.Valido
        };
    }

    private static string MensagemDe(ResultadoValidacaoCrmv resultado) => resultado switch
    {
        ResultadoValidacaoCrmv.Valido => "Registro ativo e regular no conselho regional.",
        ResultadoValidacaoCrmv.Invalido => "Registro nao localizado no conselho regional ou UF divergente.",
        ResultadoValidacaoCrmv.Suspenso => "Registro localizado, porem suspenso no conselho regional.",
        _ => "Conselho regional indisponivel no momento. O perfil permanece pendente de validacao."
    };
}
