using Vetly.API.Controllers;
using Vetly.Application.DTOs.Marketplace;
using Vetly.Application.Interfaces;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links de um item do catálogo: resgatar só para o Responsável e só com o item
/// disponível (RN-108); manter o catálogo só para o Admin.
/// </summary>
public sealed class LinksDeItemMarketplace : ILinksDoRecurso<ItemMarketplaceDto>
{
    private readonly ConstrutorDeLinks _links;
    private readonly IUsuarioAtual _usuario;

    /// <summary>Cria o fornecedor com o perfil do token da requisição.</summary>
    public LinksDeItemMarketplace(ConstrutorDeLinks links, IUsuarioAtual usuario)
    {
        _links = links;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public IEnumerable<Link> Para(ItemMarketplaceDto item)
    {
        var id = new { id = item.Id };

        yield return _links.Para<MarketplaceController>("self", HttpMethods.Get, nameof(MarketplaceController.ObterItem), id);

        // Sem estoque ou fora do catalogo, o botao de resgatar so levaria a um 422.
        // O saldo nao entra aqui: o detalhe ja diz quanto falta, e esconder o link por
        // saldo faria o item parecer indisponivel para todos.
        if (_usuario.EhTutor && item.Disponivel)
            yield return _links.Para<MarketplaceController>(
                "resgatar", HttpMethods.Post, nameof(MarketplaceController.Resgatar), id);

        if (_usuario.EhAdmin)
        {
            yield return _links.Para<MarketplaceController>(
                "atualizar", HttpMethods.Put, nameof(MarketplaceController.AtualizarItem), id);

            if (item.Ativo)
                yield return _links.Para<MarketplaceController>(
                    "desativar", HttpMethods.Delete, nameof(MarketplaceController.DesativarItem), id);
        }
    }
}
