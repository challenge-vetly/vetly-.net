using Vetly.Application.DTOs.Documento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory do <see cref="TipoDocumento.Atestado"/>.
///
/// O subtipo muda o texto, e não só o rótulo (RN-086): atestado de saúde afirma a
/// condição clínica, o de óbito registra a morte e o de vacinação comprova as doses
/// aplicadas. Emitir os três com o mesmo corpo seria emitir um documento que não diz
/// o que precisa dizer.
/// </summary>
public class AtestadoFactory : IDocumentoFactory
{
    /// <inheritdoc/>
    public TipoDocumento TipoSuportado => TipoDocumento.Atestado;

    /// <inheritdoc/>
    public Documento Criar(ContextoDoDocumentoDto contexto)
    {
        var documento = new Documento(
            tipo: TipoDocumento.Atestado,
            crmvSignatario: contexto.Crmv,
            consultaId: contexto.ConsultaId);

        // Sem subtipo informado, o de saúde é o caso comum e o mais conservador:
        // não afirma óbito nem comprova vacina que pode não ter sido aplicada.
        var subtipo = contexto.SubtipoAtestado ?? TipoAtestado.Saude;
        documento.DefinirSubtipoAtestado(subtipo);

        documento.RegistrarConteudo(BlocosDoDocumento.Juntar(
            BlocosDoDocumento.Cabecalho(contexto, $"Atestado Veterinario de {subtipo}"),
            Declaracao(subtipo, contexto),
            BlocosDoDocumento.Secao("ACHADOS DO EXAME", contexto.Conteudo.ExameFisico),
            BlocosDoDocumento.Lista("HIPOTESES DIAGNOSTICAS", contexto.Conteudo.HipotesesDiagnosticas),
            BlocosDoDocumento.Secao("OBSERVACOES", contexto.Conteudo.Orientacoes),
            BlocosDoDocumento.Rodape(contexto)));

        return documento;
    }

    /// <summary>O que cada subtipo de fato declara (RN-086).</summary>
    private static string Declaracao(TipoAtestado subtipo, ContextoDoDocumentoDto c) => subtipo switch
    {
        TipoAtestado.Obito =>
            $"Atesto, para os devidos fins, o obito do animal {c.AnimalNome}, " +
            $"constatado no atendimento de {c.DataDoAtendimento:dd/MM/yyyy}.",

        TipoAtestado.Vacinacao =>
            $"Atesto, para os devidos fins, que o animal {c.AnimalNome} foi atendido em " +
            $"{c.DataDoAtendimento:dd/MM/yyyy} e teve sua situacao vacinal avaliada, " +
            $"conforme registro abaixo.",

        _ =>
            $"Atesto, para os devidos fins, que o animal {c.AnimalNome} foi submetido a exame " +
            $"clinico em {c.DataDoAtendimento:dd/MM/yyyy}, conforme achados abaixo."
    };
}
