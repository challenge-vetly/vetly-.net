using Vetly.API.Controllers;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.Interfaces;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links de um animal: as telas que partem do pet (board, avatar, obrigações,
/// histórico clínico, documentos, consultas) e as ações que o perfil pode fazer.
///
/// Quem recebe o animal já passou pela guarda de leitura (RN-105), então a navegação
/// vai para todos. Alterar o cadastro é do dono e do Admin; registrar o peso também é
/// do veterinário que atende (RN-081) — e só por isso o link de peso aparece para ele.
/// </summary>
public sealed class LinksDeAnimal : ILinksDoRecurso<AnimalDto>
{
    private readonly ConstrutorDeLinks _links;
    private readonly IUsuarioAtual _usuario;

    /// <summary>Cria o fornecedor com o perfil do token da requisição.</summary>
    public LinksDeAnimal(ConstrutorDeLinks links, IUsuarioAtual usuario)
    {
        _links = links;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public IEnumerable<Link> Para(AnimalDto animal)
    {
        var id = new { id = animal.Id };

        yield return _links.Para<AnimaisController>("self", HttpMethods.Get, nameof(AnimaisController.ObterPorId), id);
        yield return _links.Para<AnimaisController>("board", HttpMethods.Get, nameof(AnimaisController.ObterBoard), id);
        yield return _links.Para<AnimaisController>("avatar", HttpMethods.Get, nameof(AnimaisController.ObterAvatar), id);
        yield return _links.Para<AnimaisController>("obrigacoes", HttpMethods.Get, nameof(AnimaisController.ObterObrigacoes), id);
        yield return _links.Para<AnimaisController>("prontuarios", HttpMethods.Get, nameof(AnimaisController.ObterProntuarios), id);
        yield return _links.Para<AnimaisController>("exames", HttpMethods.Get, nameof(AnimaisController.ObterExames), id);
        yield return _links.Para<DocumentosController>(
            "documentos", HttpMethods.Get, nameof(DocumentosController.ObterBoardDoPet), new { animalId = animal.Id });
        yield return _links.Para<ConsultasController>(
            "consultas", HttpMethods.Get, nameof(ConsultasController.ObterPorAnimal), new { animalId = animal.Id });

        var dono = _usuario.EhAdmin || (_usuario.EhTutor && _usuario.TutorId == animal.TutorId);

        if (dono)
        {
            yield return _links.Para<AnimaisController>("atualizar", HttpMethods.Put, nameof(AnimaisController.Atualizar), id);
            yield return _links.Para<AnimaisController>("desativar", HttpMethods.Delete, nameof(AnimaisController.Desativar), id);
            yield return _links.Para<AnimaisController>("acessos", HttpMethods.Get, nameof(AnimaisController.ObterAcessos), id);
        }

        if (dono || _usuario.EhVeterinario)
            yield return _links.Para<AnimaisController>(
                "registrar-peso", HttpMethods.Put, nameof(AnimaisController.RegistrarPeso), id);
    }
}
