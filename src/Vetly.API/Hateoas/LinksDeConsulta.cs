using Vetly.API.Controllers;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links de uma consulta, pelo estado na máquina de estados (RN-035/RN-038) e pelo
/// perfil de quem pede.
///
/// <list type="bullet">
/// <item><b>Confirmada</b>: o Responsável (e o Admin) pode cancelar, simular o
/// cancelamento antes e remarcar (RN-013/RN-014/RN-043); o veterinário pode ler o
/// briefing e iniciar o atendimento (RN-008).</item>
/// <item><b>Realizada</b>: documentos emitidos para todos; avaliar só para o
/// Responsável, que é quem avalia (RN-055).</item>
/// <item><b>Cancelada</b> (e demais estados terminais): só <c>self</c>. Nada mais a
/// fazer com ela.</item>
/// </list>
///
/// O link aparece só quando a ação é <i>possível</i> — e não quando é garantida. O
/// limite de duas remarcações, por exemplo, não está no DTO, e o servidor continua
/// sendo quem decide: o link reduz botão inútil, não substitui a regra.
/// </summary>
public sealed class LinksDeConsulta : ILinksDoRecurso<ConsultaDto>
{
    private readonly ConstrutorDeLinks _links;
    private readonly IUsuarioAtual _usuario;

    /// <summary>Cria o fornecedor com o perfil do token da requisição.</summary>
    public LinksDeConsulta(ConstrutorDeLinks links, IUsuarioAtual usuario)
    {
        _links = links;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public IEnumerable<Link> Para(ConsultaDto consulta)
    {
        var id = new { id = consulta.Id };

        yield return _links.Para<ConsultasController>("self", HttpMethods.Get, nameof(ConsultasController.ObterPorId), id);

        switch (consulta.Status)
        {
            case StatusConsulta.Confirmada:
                if (_usuario.EhTutor || _usuario.EhAdmin)
                {
                    yield return _links.Para<ConsultasController>(
                        "simular-cancelamento", HttpMethods.Get, nameof(ConsultasController.SimularCancelamento), id);
                    yield return _links.Para<ConsultasController>(
                        "cancelar", HttpMethods.Delete, nameof(ConsultasController.Cancelar), id);
                    yield return _links.Para<ConsultasController>(
                        "remarcar", HttpMethods.Post, nameof(ConsultasController.Remarcar), id);
                }

                if (_usuario.EhVeterinario || _usuario.EhAdmin)
                {
                    yield return _links.Para<ConsultasController>(
                        "briefing", HttpMethods.Get, nameof(ConsultasController.ObterBriefing), id);
                    yield return _links.Para<ConsultasController>(
                        "iniciar", HttpMethods.Post, nameof(ConsultasController.Iniciar), id);
                }
                break;

            case StatusConsulta.Realizada:
                var daConsulta = new { consultaId = consulta.Id };

                yield return _links.Para<DocumentosController>(
                    "documentos", HttpMethods.Get, nameof(DocumentosController.ObterPorConsulta), daConsulta);

                // So o Responsavel avalia: o Admin nao foi atendido, e o vet nao se avalia.
                if (_usuario.EhTutor)
                    yield return _links.Para<AvaliacoesController>(
                        "avaliar", HttpMethods.Post, nameof(AvaliacoesController.Avaliar), daConsulta);
                break;
        }
    }
}
