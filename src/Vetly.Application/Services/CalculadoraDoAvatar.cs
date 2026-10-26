using System.Globalization;
using System.Text;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>
/// Junta os dados que o cálculo do avatar precisa — obrigações, última consulta
/// realizada e data do peso — e aplica <see cref="CondicaoDoAvatar"/> (RN-112/RN-113).
///
/// <para>
/// Não aplica guarda de acesso: quem chama já garantiu a leitura do animal. Existe
/// separada para que o board e a rota <c>/avatar</c> mostrem exatamente o mesmo
/// avatar sem que o board passe pela guarda duas vezes — o que gravaria dois
/// registros na trilha da colmeia para uma única tela aberta.
/// </para>
/// </summary>
public sealed class CalculadoraDoAvatar
{
    private readonly IObrigacaoRepository _obrigacoes;
    private readonly IConsultaRepository _consultas;

    /// <summary>Cria a calculadora sobre os repositórios de obrigações e consultas.</summary>
    public CalculadoraDoAvatar(IObrigacaoRepository obrigacoes, IConsultaRepository consultas)
    {
        _obrigacoes = obrigacoes;
        _consultas = consultas;
    }

    /// <summary>Calcula o avatar do animal no instante informado.</summary>
    public async Task<AvatarDoPetDto> CalcularAsync(Animal animal, DateTime agora)
    {
        // Arquivadas entram: nao pesam como vencidas, mas um retorno cumprido no prazo
        // se arquiva sozinho e ainda conta para o bonus de humor
        var obrigacoes = await _obrigacoes.ObterDoAnimalAsync(animal.Id, incluirArquivadas: true);
        var ultimaConsulta = await _consultas.ObterDataDaUltimaRealizadaAsync(animal.Id);

        var condicao = CondicaoDoAvatar.Calcular(obrigacoes, ultimaConsulta, animal.PesoRegistradoEm, agora);

        return new AvatarDoPetDto
        {
            AnimalId = animal.Id,
            Saude = condicao.Saude,
            Humor = condicao.Humor,
            Estado = condicao.Estado,
            NivelDoPelo = condicao.NivelDoPelo,
            Motivos = [.. condicao.Motivos],
            Falas = [.. condicao.Falas],
            SpriteDisponivel = EhCachorro(animal.Especie),
            CalculadoEm = agora
        };
    }

    /// <summary>
    /// Só cachorro tem sprite no MVP (RN-096). A espécie é texto livre no cadastro, e
    /// o app já gravou "Canino", "cachorro" e "Cão": compara sem caixa e sem acento.
    /// </summary>
    internal static bool EhCachorro(string? especie)
    {
        if (string.IsNullOrWhiteSpace(especie))
            return false;

        var normalizada = SemAcento(especie.Trim().ToLowerInvariant());
        return normalizada is "cachorro" or "canino" or "canina" or "cao" or "cadela";
    }

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var semMarcas = decomposto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);
        return new string([.. semMarcas]).Normalize(NormalizationForm.FormC);
    }
}
