using Vetly.API.Controllers;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Interfaces;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links de um pagamento: o próprio, o status para o polling do app (RN-006), a
/// consulta que ele paga e — só para o Admin, que é quem a policy deixa — o
/// processamento do split (RN-070).
/// </summary>
public sealed class LinksDePagamento : ILinksDoRecurso<PagamentoDto>
{
    private readonly ConstrutorDeLinks _links;
    private readonly IUsuarioAtual _usuario;

    /// <summary>Cria o fornecedor com o perfil do token da requisição.</summary>
    public LinksDePagamento(ConstrutorDeLinks links, IUsuarioAtual usuario)
    {
        _links = links;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public IEnumerable<Link> Para(PagamentoDto pagamento)
    {
        var id = new { id = pagamento.Id };

        yield return _links.Para<PagamentosController>("self", HttpMethods.Get, nameof(PagamentosController.ObterPorId), id);
        yield return _links.Para<PagamentosController>("status", HttpMethods.Get, nameof(PagamentosController.ObterStatus), id);

        if (pagamento.ConsultaId is not { } consultaId)
            yield break;

        yield return _links.Para<ConsultasController>(
            "consulta", HttpMethods.Get, nameof(ConsultasController.ObterPorId), new { id = consultaId });

        // Split so existe para pagamento de consulta (PAGAMENTO-001) e so o Admin processa.
        if (_usuario.EhAdmin)
            yield return _links.Para<PagamentosController>(
                "processar-split", HttpMethods.Post, nameof(PagamentosController.ProcessarSplit), id);
    }
}
