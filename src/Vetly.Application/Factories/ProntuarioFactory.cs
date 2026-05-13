using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory responsável pela criação de documentos do tipo <see cref="TipoDocumento.Prontuario"/>.
/// Registrada automaticamente via IEnumerable&lt;IDocumentoFactory&gt; no DI container.
/// </summary>
public class ProntuarioFactory : IDocumentoFactory
{
    /// <inheritdoc/>
    public TipoDocumento TipoSuportado => TipoDocumento.Prontuario;

    /// <inheritdoc/>
    public Documento Criar(ConsultaDto consulta, VeterinarioDto veterinario, AnimalDto animal)
    {
        // O prontuário é vinculado à consulta e assinado pelo CRMV do veterinário responsável
        return new Documento(
            tipo: TipoDocumento.Prontuario,
            crmvSignatario: veterinario.Crmv,
            consultaId: consulta.Id
        );
    }
}
