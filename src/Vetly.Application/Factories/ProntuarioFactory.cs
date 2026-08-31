using Vetly.Application.DTOs.Documento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory do <see cref="TipoDocumento.Prontuario"/>.
///
/// É o documento mais completo: registra o atendimento inteiro, na ordem clínica
/// (anamnese → exame → hipóteses → conduta → orientações). Sai do estado final
/// aprovado pelo veterinário (RN-082/RN-083).
/// </summary>
public class ProntuarioFactory : IDocumentoFactory
{
    /// <inheritdoc/>
    public TipoDocumento TipoSuportado => TipoDocumento.Prontuario;

    /// <inheritdoc/>
    public Documento Criar(ContextoDoDocumentoDto contexto)
    {
        var documento = new Documento(
            tipo: TipoDocumento.Prontuario,
            crmvSignatario: contexto.Crmv,
            consultaId: contexto.ConsultaId);

        var conteudo = contexto.Conteudo;

        documento.RegistrarConteudo(BlocosDoDocumento.Juntar(
            BlocosDoDocumento.Cabecalho(contexto, "Prontuario Veterinario"),
            BlocosDoDocumento.Secao("ANAMNESE", conteudo.Anamnese),
            BlocosDoDocumento.Secao("EXAME FISICO", conteudo.ExameFisico),
            BlocosDoDocumento.Lista("HIPOTESES DIAGNOSTICAS", conteudo.HipotesesDiagnosticas),
            BlocosDoDocumento.Secao("CONDUTA", conteudo.Conduta),
            BlocosDoDocumento.Secao("ORIENTACOES AO RESPONSAVEL", conteudo.Orientacoes),
            BlocosDoDocumento.Rodape(contexto)));

        return documento;
    }
}
