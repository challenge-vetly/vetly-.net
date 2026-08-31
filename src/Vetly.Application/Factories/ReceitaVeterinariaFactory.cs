using Vetly.Application.DTOs.Documento;
using Vetly.Application.Exceptions;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory do <see cref="TipoDocumento.ReceitaVeterinaria"/>.
///
/// Exige diagnóstico validado (RN-082) e assinatura (RN-087). A prescrição sai da
/// conduta aprovada pelo veterinário — receita sem conduta não é receita, e por isso
/// a factory recusa em vez de emitir um documento vazio que pareceria válido.
/// </summary>
public class ReceitaVeterinariaFactory : IDocumentoFactory
{
    /// <inheritdoc/>
    public TipoDocumento TipoSuportado => TipoDocumento.ReceitaVeterinaria;

    /// <inheritdoc/>
    public Documento Criar(ContextoDoDocumentoDto contexto)
    {
        if (string.IsNullOrWhiteSpace(contexto.Conteudo.Conduta))
            throw new BusinessRuleException("RN-083",
                "A receita exige a conduta aprovada pelo veterinario. Nao ha prescricao a emitir.");

        var documento = new Documento(
            tipo: TipoDocumento.ReceitaVeterinaria,
            crmvSignatario: contexto.Crmv,
            consultaId: contexto.ConsultaId);

        // O peso entra na receita porque dose se calcula sobre ele: quem dispensa o
        // medicamento precisa poder conferir a posologia (RN-081).
        var alertaDePeso = contexto.PesoKg is null or <= 0
            ? "ATENCAO: peso do animal nao registrado no atendimento. Confira a posologia."
            : string.Empty;

        documento.RegistrarConteudo(BlocosDoDocumento.Juntar(
            BlocosDoDocumento.Cabecalho(contexto, "Receituario Veterinario"),
            "USO VETERINARIO",
            alertaDePeso,
            BlocosDoDocumento.Secao("PRESCRICAO", contexto.Conteudo.Conduta),
            BlocosDoDocumento.Secao("ORIENTACOES AO RESPONSAVEL", contexto.Conteudo.Orientacoes),
            BlocosDoDocumento.Rodape(contexto)));

        return documento;
    }
}
