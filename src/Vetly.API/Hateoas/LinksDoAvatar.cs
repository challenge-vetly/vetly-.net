using Vetly.API.Controllers;
using Vetly.Application.DTOs.Animal;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links do avatar: o board do pet, as obrigações — é delas que sai cada motivo, e é
/// lá que o Responsável resolve o que derrubou a saúde (RN-112) — e o histórico.
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
        yield return _links.Para<AnimaisController>("historico", HttpMethods.Get, nameof(AnimaisController.ObterHistoricoDoAvatar), id);
    }
}

/// <summary>
/// Links de um dia do histórico do avatar: o avatar de hoje, para o app ir do gráfico
/// ao estado atual sem montar a rota.
/// </summary>
public sealed class LinksDoRegistroDoAvatar : ILinksDoRecurso<RegistroDiarioDoAvatarDto>
{
    private readonly ConstrutorDeLinks _links;

    /// <summary>Cria o fornecedor de links do histórico.</summary>
    public LinksDoRegistroDoAvatar(ConstrutorDeLinks links) => _links = links;

    /// <inheritdoc/>
    public IEnumerable<Link> Para(RegistroDiarioDoAvatarDto registro)
    {
        yield return _links.Para<AnimaisController>(
            "avatar", HttpMethods.Get, nameof(AnimaisController.ObterAvatar), new { id = registro.AnimalId });
    }
}
