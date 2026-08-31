using System.Text;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Gerador de PDF sem dependência externa (RN-090, §11).
///
/// Escreve um PDF 1.4 à mão, com Helvetica — uma das 14 fontes que todo leitor de PDF
/// já traz, o que dispensa embutir arquivo de fonte. É deliberadamente simples: só
/// texto, paginado, sem imagem nem tabela.
///
/// A alternativa era trazer uma biblioteca de PDF para o projeto. Para o que o MVP
/// precisa — um documento clínico legível que o Responsável leva para outra clínica —
/// isso seria adicionar infraestrutura sem necessidade (§11). Quando o documento
/// ganhar identidade visual e QR de verificação, troca-se a implementação desta porta
/// sem mexer no resto.
/// </summary>
public class GeradorDePdfSimples : IGeradorDePdf
{
    // A4 em pontos (72 dpi), o padrão de página do PDF
    private const int LarguraDaPagina = 595;
    private const int AlturaDaPagina = 842;

    private const int Margem = 56;
    private const int TamanhoDaFonte = 10;
    private const int AlturaDaLinha = 14;

    /// <summary>Caracteres por linha antes da quebra, no corpo em Helvetica 10.</summary>
    private const int ColunasPorLinha = 92;

    /// <summary>Linhas que cabem numa página, descontadas as margens.</summary>
    private static readonly int LinhasPorPagina = (AlturaDaPagina - 2 * Margem) / AlturaDaLinha;

    /// <inheritdoc/>
    public byte[] Renderizar(string titulo, string conteudo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conteudo);

        var paginas = Paginar(QuebrarLinhas(conteudo));

        return Montar(titulo, paginas);
    }

    /// <summary>
    /// Quebra o texto em linhas que cabem na largura útil, sem partir palavra ao meio
    /// — palavra cortada em documento clínico é o tipo de detalhe que faz o leitor
    /// duvidar do resto.
    /// </summary>
    private static List<string> QuebrarLinhas(string conteudo)
    {
        var linhas = new List<string>();

        foreach (var original in conteudo.Replace("\r\n", "\n").Split('\n'))
        {
            if (original.Length <= ColunasPorLinha)
            {
                linhas.Add(original);
                continue;
            }

            var atual = new StringBuilder();

            foreach (var palavra in original.Split(' '))
            {
                // Palavra maior que a linha inteira: aí não há como não cortar
                if (palavra.Length > ColunasPorLinha)
                {
                    if (atual.Length > 0)
                    {
                        linhas.Add(atual.ToString());
                        atual.Clear();
                    }

                    for (var i = 0; i < palavra.Length; i += ColunasPorLinha)
                        linhas.Add(palavra.Substring(i, Math.Min(ColunasPorLinha, palavra.Length - i)));

                    continue;
                }

                if (atual.Length + palavra.Length + 1 > ColunasPorLinha)
                {
                    linhas.Add(atual.ToString());
                    atual.Clear();
                }

                if (atual.Length > 0)
                    atual.Append(' ');

                atual.Append(palavra);
            }

            if (atual.Length > 0)
                linhas.Add(atual.ToString());
        }

        return linhas;
    }

    private static List<List<string>> Paginar(List<string> linhas)
    {
        var paginas = new List<List<string>>();

        for (var i = 0; i < linhas.Count; i += LinhasPorPagina)
            paginas.Add(linhas.GetRange(i, Math.Min(LinhasPorPagina, linhas.Count - i)));

        // Conteúdo em branco não chega aqui, mas um PDF sem página nenhuma é inválido
        if (paginas.Count == 0)
            paginas.Add([string.Empty]);

        return paginas;
    }

    /// <summary>
    /// Monta o arquivo: objetos numerados, tabela de referência cruzada com o
    /// deslocamento de cada um, e o trailer apontando para a raiz. Um leitor de PDF
    /// abre pelo fim, então a tabela precisa dos bytes exatos — daí montar tudo sobre
    /// um único buffer.
    /// </summary>
    private static byte[] Montar(string titulo, List<List<string>> paginas)
    {
        var buffer = new MemoryStream();
        var deslocamentos = new List<long>();

        void Escrever(string texto) => buffer.Write(Latin1(texto));

        void Objeto(int numero, string corpo)
        {
            deslocamentos.Add(buffer.Length);
            Escrever($"{numero} 0 obj\n{corpo}\nendobj\n");
        }

        Escrever("%PDF-1.4\n");

        // 1 = catálogo, 2 = páginas, 3 = fonte; daí em diante, par a par: página e conteúdo
        var primeiraPagina = 4;
        var idsDasPaginas = string.Join(" ",
            Enumerable.Range(0, paginas.Count).Select(i => $"{primeiraPagina + i * 2} 0 R"));

        Objeto(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Objeto(2, $"<< /Type /Pages /Kids [{idsDasPaginas}] /Count {paginas.Count} >>");
        Objeto(3, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        for (var i = 0; i < paginas.Count; i++)
        {
            var idDaPagina = primeiraPagina + i * 2;
            var idDoConteudo = idDaPagina + 1;

            Objeto(idDaPagina,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {LarguraDaPagina} {AlturaDaPagina}] " +
                $"/Resources << /Font << /F1 3 0 R >> >> /Contents {idDoConteudo} 0 R >>");

            var fluxo = FluxoDeTexto(paginas[i]);
            var bytes = Latin1(fluxo);

            deslocamentos.Add(buffer.Length);
            Escrever($"{idDoConteudo} 0 obj\n<< /Length {bytes.Length} >>\nstream\n");
            buffer.Write(bytes);
            Escrever("endstream\nendobj\n");
        }

        // Metadados: o título é o que o leitor mostra na aba
        var idDoInfo = primeiraPagina + paginas.Count * 2;
        Objeto(idDoInfo, $"<< /Title ({Escapar(titulo)}) /Producer (Vetly) >>");

        var inicioDaTabela = buffer.Length;
        var total = deslocamentos.Count + 1;

        Escrever($"xref\n0 {total}\n0000000000 65535 f \n");
        foreach (var deslocamento in deslocamentos)
            Escrever($"{deslocamento:D10} 00000 n \n");

        Escrever(
            $"trailer\n<< /Size {total} /Root 1 0 R /Info {idDoInfo} 0 R >>\n" +
            $"startxref\n{inicioDaTabela}\n%%EOF\n");

        return buffer.ToArray();
    }

    /// <summary>Desenha as linhas da página, de cima para baixo.</summary>
    private static string FluxoDeTexto(List<string> linhas)
    {
        var fluxo = new StringBuilder();

        fluxo.Append("BT\n");
        fluxo.Append($"/F1 {TamanhoDaFonte} Tf\n");
        fluxo.Append($"{AlturaDaLinha} TL\n");
        fluxo.Append($"{Margem} {AlturaDaPagina - Margem} Td\n");

        foreach (var linha in linhas)
            fluxo.Append($"({Escapar(linha)}) Tj T*\n");

        fluxo.Append("ET\n");

        return fluxo.ToString();
    }

    /// <summary>
    /// Parênteses e barra invertida delimitam string no PDF: sem escapar, um nome de
    /// medicamento com parêntese corromperia o arquivo inteiro.
    /// </summary>
    private static string Escapar(string texto) => texto
        .Replace("\\", "\\\\")
        .Replace("(", "\\(")
        .Replace(")", "\\)");

    /// <summary>
    /// WinAnsi (Latin-1) é o que a fonte declara. Caractere fora dela vira '?' em vez
    /// de virar byte inválido no fluxo.
    /// </summary>
    private static byte[] Latin1(string texto) =>
        Encoding.GetEncoding(28591, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback)
            .GetBytes(texto);
}
