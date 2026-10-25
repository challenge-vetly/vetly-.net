using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Vetly.API.Hateoas;

/// <summary>
/// Declara o corpo da resposta 200 quando a rota é pedida com hipermídia.
///
/// <para>
/// Existe porque <c>[ProducesResponseType]</c> não resolve este caso: o ApiExplorer
/// guarda um tipo por status, e um segundo atributo 200 com
/// <c>application/hal+json</c> <b>substitui</b> o primeiro em vez de somar — o
/// documento passaria a dizer que a rota só devolve o envelope com links, que é o
/// contrário do padrão. Este atributo é lido pelo <see cref="TransformadorHalOpenApi"/>,
/// que acrescenta o segundo media type ao mesmo 200.
/// </para>
/// </summary>
/// <param name="tipo">Envelope devolvido: <c>Recurso&lt;T&gt;</c> ou <c>RecursoPaginado&lt;T&gt;</c>.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RespostaHalAttribute(Type tipo) : Attribute
{
    /// <summary>Envelope devolvido com <c>application/hal+json</c>.</summary>
    public Type Tipo { get; } = tipo;
}

/// <summary>
/// Documenta no OpenAPI a variante hal+json das rotas marcadas com
/// <see cref="RespostaHalAttribute"/>: o segundo corpo do 200 e o parâmetro
/// <c>?hateoas=true</c>, que não é parâmetro de action e por isso o gerador não vê.
/// </summary>
public sealed class TransformadorHalOpenApi : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public async Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var marca = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<RespostaHalAttribute>()
            .FirstOrDefault();

        if (marca is null || operation.Responses?.TryGetValue("200", out var ok) != true || ok is null)
            return;

        var esquema = await context.GetOrCreateSchemaAsync(marca.Tipo, null, cancellationToken);

        if (ok is OpenApiResponse resposta)
        {
            resposta.Content ??= new Dictionary<string, OpenApiMediaType>();
            resposta.Content[NegociacaoHateoas.MediaTypeHal] = new OpenApiMediaType { Schema = esquema };
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = NegociacaoHateoas.ParametroDeQuery,
            In = ParameterLocation.Query,
            Required = false,
            Description = "true devolve o envelope com _links (equivale a Accept: application/hal+json).",
            Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean }
        });
    }
}
