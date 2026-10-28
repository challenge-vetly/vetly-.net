namespace Vetly.Application.DTOs.Comum;

/// <summary>
/// Parâmetros de ordenação das listagens (<c>?ordenarPor=&amp;direcao=asc|desc</c>).
///
/// <para>
/// Segue o mesmo contrato da <see cref="Paginacao"/>: valor desconhecido é
/// <b>normalizado</b>, nunca rejeitado. Ordenação é conveniência de leitura, e
/// devolver 400 porque o app pediu um campo que a API ainda não conhece quebraria a
/// tela por algo que não é erro do Responsável.
/// </para>
/// <para>
/// <b>Por que whitelist, e não o nome da coluna.</b> Ordenar por uma propriedade
/// escolhida livremente pelo cliente é montar expressão a partir de entrada externa:
/// o cliente passaria a escolher o que vira <c>ORDER BY</c>, inclusive campos que a
/// listagem não deveria nem revelar (ordenar por <c>SenhaHash</c> vaza a ordem dos
/// hashes). Cada endpoint declara os campos que aceita; o que estiver fora da lista
/// cai no padrão do endpoint. Quem traduz o campo em expressão é o repositório, num
/// <c>switch</c> fechado — nunca reflexão sobre o texto recebido.
/// </para>
/// </summary>
public class Ordenacao
{
    private readonly string? _ordenarPor;
    private readonly string? _direcao;

    /// <summary>
    /// Campo de ordenação pedido pelo cliente, sem distinção de caixa. Só vale se
    /// constar da whitelist do endpoint (ver <see cref="Resolver"/>).
    /// </summary>
    public string? OrdenarPor
    {
        get => _ordenarPor;
        init => _ordenarPor = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// <c>asc</c> ou <c>desc</c>. Qualquer outro valor é tratado como ausente e cai na
    /// direção padrão do endpoint.
    /// </summary>
    public string? Direcao
    {
        get => _direcao;
        init => _direcao = string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Resolve o pedido do cliente contra a whitelist do endpoint.
    /// </summary>
    /// <param name="permitidos">Campos que o endpoint sabe ordenar, em minúsculas.</param>
    /// <param name="campoPadrao">Campo usado quando o pedido é vazio ou desconhecido.</param>
    /// <param name="descendentePadrao">Direção usada quando o pedido não diz qual.</param>
    /// <returns>
    /// O campo canônico (sempre um dos <paramref name="permitidos"/>) e a direção.
    /// </returns>
    public CriterioDeOrdenacao Resolver(
        IReadOnlyCollection<string> permitidos, string campoPadrao, bool descendentePadrao)
    {
        var pedido = _ordenarPor?.ToLowerInvariant();

        var campo = pedido is not null && permitidos.Contains(pedido) ? pedido : campoPadrao;

        var descendente = _direcao switch
        {
            "asc" => false,
            "desc" => true,
            _ => descendentePadrao
        };

        return new CriterioDeOrdenacao(campo, descendente);
    }
}

/// <summary>
/// Ordenação já resolvida contra a whitelist: o campo é garantidamente um dos aceitos
/// pelo endpoint, e o repositório pode traduzi-lo sem conferir de novo.
/// </summary>
/// <param name="Campo">Campo canônico, em minúsculas.</param>
/// <param name="Descendente">Verdadeiro para ordem decrescente.</param>
public sealed record CriterioDeOrdenacao(string Campo, bool Descendente)
{
    /// <summary>Direção em texto, como volta nos links de paginação.</summary>
    public string Direcao => Descendente ? "desc" : "asc";
}
