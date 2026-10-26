using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Comum;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Histórico diário da condição do avatar (RN-114) — o primeiro dado da Vetly em
/// banco de documentos.
///
/// <para>
/// <b>Por que documento, e não Oracle.</b> É uma série temporal de leitura: um
/// registro por animal por dia, gravado por upsert e lido em ordem de data, sem
/// junção com nada e sem transação com o resto do domínio. Os motivos são uma lista
/// de tamanho variável, que no relacional viraria tabela filha só para ser lida
/// junto. Num banco de documentos o registro vai inteiro, e o índice único
/// (<c>animalId</c>, <c>data</c>) garante um por dia sem lógica na aplicação.
/// </para>
/// <para>
/// O catálogo do marketplace, ao contrário, ficou no Oracle: ele participa da mesma
/// transação que debita pontos e emite o cupom, e precisa do token de concorrência
/// no estoque. Dado transacional fica no relacional; dado de leitura derivado, aqui.
/// </para>
/// </summary>
public interface IHistoricoDoAvatarRepository
{
    /// <summary>
    /// Grava a condição do dia por upsert: a segunda gravação do mesmo dia substitui a
    /// primeira, e o histórico fica com a foto mais recente de cada dia.
    /// </summary>
    Task RegistrarAsync(RegistroDiarioDoAvatarDto registro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Página do histórico de um animal, ordenada no banco antes do recorte.
    /// </summary>
    /// <param name="animalId">Animal do histórico.</param>
    /// <param name="paginacao">Página pedida.</param>
    /// <param name="ordenacao">Critério já resolvido contra <see cref="CamposDeOrdenacao.HistoricoDoAvatar"/>.</param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    Task<ResultadoPaginado<RegistroDiarioDoAvatarDto>> ObterPaginadoAsync(
        Guid animalId, Paginacao paginacao, CriterioDeOrdenacao ordenacao, CancellationToken cancellationToken = default);
}
