using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory responsável pela criação de documentos do tipo <see cref="TipoDocumento.ReceitaVeterinaria"/>.
/// Receitas só podem ser geradas por veterinário com diagnóstico validado (RN-024).
/// </summary>
public class ReceitaVeterinariaFactory : IDocumentoFactory
{
    /// <inheritdoc/>
    public TipoDocumento TipoSuportado => TipoDocumento.ReceitaVeterinaria;

    /// <inheritdoc/>
    public Documento Criar(ConsultaDto consulta, VeterinarioDto veterinario, AnimalDto animal)
    {
        return new Documento(
            tipo: TipoDocumento.ReceitaVeterinaria,
            crmvSignatario: veterinario.Crmv,
            consultaId: consulta.Id
        );
    }
}
