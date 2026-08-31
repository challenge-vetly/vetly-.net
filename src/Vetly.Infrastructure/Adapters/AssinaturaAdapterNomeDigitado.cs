using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Assinatura pelo nome digitado, para o MVP (RN-087, §5).
///
/// O profissional digita o próprio nome no ato de assinar, e o sistema confere contra
/// o nome registrado. Não é assinatura com validade jurídica plena: é registro
/// auditável de que uma pessoa identificada, com CRMV, afirmou aquele conteúdo em
/// determinado momento.
///
/// A diferença aparece no carimbo, e de propósito: <b>o documento diz como foi
/// assinado</b>. Uma receita assinada assim não habilita dispensação de controlado
/// fora da plataforma, e quem a receber precisa poder ver isso sem perguntar. Omitir
/// seria deixar o documento parecer mais do que é.
/// </summary>
public class AssinaturaAdapterNomeDigitado : IAssinaturaAdapter
{
    private readonly ILogger<AssinaturaAdapterNomeDigitado> _logger;

    private static readonly CultureInfo Brasil = new("pt-BR");

    public AssinaturaAdapterNomeDigitado(ILogger<AssinaturaAdapterNomeDigitado> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task<AssinaturaDto> AssinarAsync(SolicitacaoDeAssinaturaDto solicitacao)
    {
        if (string.IsNullOrWhiteSpace(solicitacao.NomeDigitado))
            throw new ValidationException("nomeCompleto",
                "Digite seu nome completo para assinar o documento.");

        if (!Confere(solicitacao.NomeDigitado, solicitacao.NomeDoVeterinario))
        {
            // Assinar em nome de outro profissional e o risco que esta conferencia
            // existe para cobrir — ainda que a barreira, neste metodo, seja fraca
            throw new BusinessRuleException("RN-087",
                "O nome digitado nao confere com o nome registrado do profissional.");
        }

        var agora = DateTime.UtcNow;

        var carimbo =
            $"Assinado eletronicamente por {solicitacao.NomeDoVeterinario} - " +
            $"CRMV {solicitacao.Crmv}/{solicitacao.UfAtuacao} em " +
            $"{agora.ToString("dd/MM/yyyy HH:mm", Brasil)} (UTC), por nome digitado na plataforma Vetly. " +
            $"Nao habilita dispensacao de medicamento controlado fora da plataforma.";

        _logger.LogInformation(
            "Documento {DocumentoId} ({Tipo}) assinado por nome digitado | crmv={Crmv}",
            solicitacao.DocumentoId, solicitacao.Tipo, solicitacao.Crmv);

        return Task.FromResult(new AssinaturaDto(
            Metodo: "NomeDigitado",
            Carimbo: carimbo,
            AssinadoEm: agora,
            HabilitaDispensacaoExterna: false));
    }

    /// <summary>
    /// Compara ignorando caixa, acento e espaço repetido. Recusar a assinatura por
    /// causa de um acento faltando seria rigor no lugar errado — o que importa é que
    /// o profissional escreveu o próprio nome, não que o digitou byte a byte.
    /// </summary>
    private static bool Confere(string digitado, string registrado) =>
        string.Equals(Normalizar(digitado), Normalizar(registrado), StringComparison.OrdinalIgnoreCase);

    private static string Normalizar(string valor)
    {
        var semAcento = new string(valor
            .Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        return string.Join(' ', semAcento.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
