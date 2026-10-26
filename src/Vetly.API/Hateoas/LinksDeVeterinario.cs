using Vetly.API.Controllers;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Interfaces;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links de um veterinário: o que o Responsável precisa para agendar (serviços e
/// disponibilidade), a situação do CRMV, e as ações de cadastro que só o Admin faz.
///
/// A agenda (consultas futuras) só aparece para o próprio profissional e o Admin: é a
/// lista de quem ele vai atender, e um link nela convidaria qualquer Responsável a
/// olhar a agenda alheia.
/// </summary>
public sealed class LinksDeVeterinario : ILinksDoRecurso<VeterinarioDto>
{
    private readonly ConstrutorDeLinks _links;
    private readonly IUsuarioAtual _usuario;

    /// <summary>Cria o fornecedor com o perfil do token da requisição.</summary>
    public LinksDeVeterinario(ConstrutorDeLinks links, IUsuarioAtual usuario)
    {
        _links = links;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public IEnumerable<Link> Para(VeterinarioDto veterinario)
    {
        var id = new { id = veterinario.Id };

        yield return _links.Para<VeterinariosController>("self", HttpMethods.Get, nameof(VeterinariosController.ObterPorId), id);
        yield return _links.Para<VeterinariosController>(
            "servicos", HttpMethods.Get, nameof(VeterinariosController.ObterServicos), id);
        yield return _links.Para<VeterinariosController>(
            "disponibilidade", HttpMethods.Get, nameof(VeterinariosController.ObterDisponibilidade), id);
        yield return _links.Para<VeterinariosController>(
            "crmv", HttpMethods.Get, nameof(VeterinariosController.ObterSituacaoCrmv), id);

        if (_usuario.EhAdmin || _usuario.VeterinarioId == veterinario.Id)
            yield return _links.Para<VeterinariosController>(
                "agenda", HttpMethods.Get, nameof(VeterinariosController.ObterAgenda), id);

        if (!_usuario.EhAdmin)
            yield break;

        yield return _links.Para<VeterinariosController>(
            "revalidar-crmv", HttpMethods.Post, nameof(VeterinariosController.RevalidarCrmv), id);

        if (veterinario.Ativo)
            yield return _links.Para<VeterinariosController>(
                "desativar", HttpMethods.Delete, nameof(VeterinariosController.Desativar), id);
    }
}
