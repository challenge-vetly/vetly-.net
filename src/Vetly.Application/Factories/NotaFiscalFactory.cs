using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory responsável pela criação de documentos do tipo <see cref="TipoDocumento.NotaFiscal"/>.
/// Nota fiscal é gerada automaticamente ao dar alta em internação ou finalizar consulta paga.
/// </summary>
public class NotaFiscalFactory : IDocumentoFactory
{
    /// <inheritdoc/>
    public TipoDocumento TipoSuportado => TipoDocumento.NotaFiscal;

    /// <inheritdoc/>
    public Documento Criar(ConsultaDto consulta, VeterinarioDto veterinario, AnimalDto animal)
    {
        // Nota fiscal pode ser vinculada à consulta ou à internação — aqui usa consultaId
        return new Documento(
            tipo: TipoDocumento.NotaFiscal,
            crmvSignatario: veterinario.Crmv,
            consultaId: consulta.Id
        );
    }
}
