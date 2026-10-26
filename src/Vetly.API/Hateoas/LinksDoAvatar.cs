using Vetly.API.Controllers;
using Vetly.Application.DTOs.Animal;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links do avatar: o board do pet e as obrigações — é delas que sai cada motivo, e é
/// lá que o Responsável resolve o que derrubou a saúde (RN-112).
/// </summary>
public sealed class LinksDoAvatar : ILinksDoRecurso<AvatarDoPetDto>
{
    private readonly ConstrutorDeLinks _links;

    /// <summary>Cria o fornecedor de links do avatar.</summary>
    public LinksDoAvatar(ConstrutorDeLinks links) => _links = links;

    /// <inheritdoc/>
    public IEnumerable<Link> Para(AvatarDoPetDto avatar)
    {
        var id = new { id = avatar.AnimalId };

        yield return _links.Para<AnimaisController>("self", HttpMethods.Get, nameof(AnimaisController.ObterAvatar), id);
        yield return _links.Para<AnimaisController>("board", HttpMethods.Get, nameof(AnimaisController.ObterBoard), id);
        yield return _links.Para<AnimaisController>("obrigacoes", HttpMethods.Get, nameof(AnimaisController.ObterObrigacoes), id);
    }
}
