using Vetly.Application.DTOs.Documento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Contrato do Factory Pattern para criação de documentos clínicos.
/// Cada implementação sabe criar — e agora <b>formatar</b> — um tipo específico.
/// O DocumentoService seleciona a factory correta via IEnumerable&lt;IDocumentoFactory&gt;.
/// </summary>
public interface IDocumentoFactory
{
    /// <summary>
    /// Tipo de documento que esta factory é capaz de criar.
    /// Usado pelo DocumentoService para selecionar a factory adequada.
    /// </summary>
    TipoDocumento TipoSuportado { get; }

    /// <summary>
    /// Cria o documento já com o conteúdo formatado a partir do estado final aprovado
    /// pelo veterinário (RN-083). A factory formata; ela não infere nada de novo.
    /// </summary>
    Documento Criar(ContextoDoDocumentoDto contexto);
}

/// <summary>
/// Cabeçalho, rodapé e seções comuns a todo documento clínico da plataforma.
///
/// Ficam em um só lugar porque são o que identifica o documento como emitido por um
/// profissional habilitado: nome, CRMV e UF. Um documento clínico sem isso não serve
/// para nada fora do app.
/// </summary>
public static class BlocosDoDocumento
{
    /// <summary>Identificação do prestador, do animal e do Responsável.</summary>
    public static string Cabecalho(ContextoDoDocumentoDto c, string titulo)
    {
        var idade = c.IdadeAnos is { } anos ? $", {anos} ano(s)" : string.Empty;
        var peso = c.PesoKg is { } kg && kg > 0 ? $", {kg:0.##} kg" : string.Empty;
        var sexo = string.IsNullOrWhiteSpace(c.Sexo) ? string.Empty : $", {c.Sexo}";

        return $"""
            {titulo.ToUpperInvariant()}

            Emitido por: {c.VeterinarioNome} - CRMV {c.Crmv}/{c.UfAtuacao}
            Data do atendimento: {c.DataDoAtendimento:dd/MM/yyyy HH:mm} (UTC)

            Animal: {c.AnimalNome} - {c.Especie}, {c.Raca}{idade}{sexo}{peso}
            Responsavel: {c.TutorNome}
            """;
    }

    /// <summary>
    /// Rodapé com a identificação do signatário. O carimbo da assinatura é acrescentado
    /// depois, no momento em que o documento é efetivamente assinado (RN-087).
    /// </summary>
    public static string Rodape(ContextoDoDocumentoDto c) =>
        $"""
        _______________________________________
        {c.VeterinarioNome}
        CRMV {c.Crmv}/{c.UfAtuacao}
        """;

    /// <summary>Uma seção com título, omitida quando não há o que dizer.</summary>
    public static string Secao(string titulo, string? corpo) =>
        string.IsNullOrWhiteSpace(corpo) ? string.Empty : $"{titulo}\n{corpo.Trim()}\n";

    /// <summary>Uma seção em lista numerada, omitida quando a lista está vazia.</summary>
    public static string Lista(string titulo, IReadOnlyList<string> itens)
    {
        if (itens.Count == 0)
            return string.Empty;

        var linhas = itens.Select((item, i) => $"{i + 1}. {item}");

        return $"{titulo}\n{string.Join("\n", linhas)}\n";
    }

    /// <summary>Junta os blocos preenchidos, separados por linha em branco.</summary>
    public static string Juntar(params string?[] blocos) =>
        string.Join("\n", blocos.Where(b => !string.IsNullOrWhiteSpace(b)).Select(b => b!.TrimEnd()));
}
