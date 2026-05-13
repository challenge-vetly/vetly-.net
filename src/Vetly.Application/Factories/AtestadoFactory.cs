using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory responsável pela criação de documentos do tipo <see cref="TipoDocumento.Atestado"/>.
/// Atestados incluem subtipo (óbito, saúde, vacinação) armazenado no corpo do documento.
/// </summary>
public class AtestadoFactory : IDocumentoFactory
{
    /// <inheritdoc/>
    public TipoDocumento TipoSuportado => TipoDocumento.Atestado;

    /// <inheritdoc/>
    public Documento Criar(ConsultaDto consulta, VeterinarioDto veterinario, AnimalDto animal)
    {
        return new Documento(
            tipo: TipoDocumento.Atestado,
            crmvSignatario: veterinario.Crmv,
            consultaId: consulta.Id
        );
    }
}
