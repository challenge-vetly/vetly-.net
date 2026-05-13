using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Contrato do Factory Pattern para criação de documentos clínicos.
/// Cada implementação sabe criar um tipo específico de documento.
/// O DocumentoService seleciona a factory correta via IEnumerable&lt;IDocumentoFactory&gt;.
/// </summary>
public interface IDocumentoFactory
{
    /// <summary>
    /// Tipo de documento que esta factory é capaz de criar.
    /// Usado pelo DocumentoService para selecionar a factory adequada.
    /// </summary>
    TipoDocumento TipoSuportado { get; }

    /// <summary>Cria e retorna um documento clínico preenchido com os dados fornecidos.</summary>
    Documento Criar(ConsultaDto consulta, VeterinarioDto veterinario, AnimalDto animal);
}
