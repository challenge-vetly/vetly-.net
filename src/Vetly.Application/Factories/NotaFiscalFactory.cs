using Vetly.Application.DTOs.Documento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory do <see cref="TipoDocumento.NotaFiscal"/>.
///
/// É recibo de atendimento, e não documento fiscal com validade tributária: a emissão
/// fiscal de verdade depende de integração com a prefeitura, que está fora do MVP. O
/// documento diz isso em letras claras — deixar ambíguo seria pior que não emitir.
/// </summary>
public class NotaFiscalFactory : IDocumentoFactory
{
    /// <inheritdoc/>
    public TipoDocumento TipoSuportado => TipoDocumento.NotaFiscal;

    /// <inheritdoc/>
    public Documento Criar(ContextoDoDocumentoDto contexto)
    {
        var documento = new Documento(
            tipo: TipoDocumento.NotaFiscal,
            crmvSignatario: contexto.Crmv,
            consultaId: contexto.ConsultaId);

        var valor = contexto.ValorDoAtendimento is { } v ? $"R$ {v:N2}" : "nao informado";

        documento.RegistrarConteudo(BlocosDoDocumento.Juntar(
            BlocosDoDocumento.Cabecalho(contexto, "Recibo de Atendimento"),
            $"""
            Servico: atendimento veterinario ({contexto.Modalidade})
            Valor: {valor}

            Este recibo comprova o atendimento e o pagamento na plataforma Vetly.
            Nao substitui documento fiscal emitido pelo prestador.
            """,
            BlocosDoDocumento.Rodape(contexto)));

        return documento;
    }
}
