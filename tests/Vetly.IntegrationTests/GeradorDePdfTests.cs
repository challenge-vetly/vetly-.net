using System.Text;
using Vetly.Infrastructure.Adapters;

namespace Vetly.IntegrationTests;

/// <summary>
/// Renderizacao do documento clinico em PDF (RN-090).
///
/// Fica neste projeto porque o gerador vive na Infrastructure.
/// </summary>
public class GeradorDePdfTests
{
    private readonly GeradorDePdfSimples _gerador = new();

    private static string ComoTexto(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    [Fact]
    public void Pdf_TemAEstruturaQueUmLeitorEsperaEncontrar()
    {
        var pdf = _gerador.Renderizar("Prontuario - Thor", "PRONTUARIO VETERINARIO\n\nAnamnese: vomito ha 24h.");

        var texto = ComoTexto(pdf);

        // Um leitor abre o arquivo pelo fim: sem xref e trailer nao ha o que abrir
        Assert.StartsWith("%PDF-1.4", texto);
        Assert.Contains("/Type /Catalog", texto);
        Assert.Contains("xref", texto);
        Assert.Contains("trailer", texto);
        Assert.Contains("startxref", texto);
        Assert.EndsWith("%%EOF\n", texto);
    }

    [Fact]
    public void Pdf_LevaOTextoDoDocumentoEOTituloComoMetadado()
    {
        var pdf = _gerador.Renderizar("Receituario - Thor", "Ondansetrona 0,5 mg/kg a cada 12h");

        var texto = ComoTexto(pdf);

        Assert.Contains("Ondansetrona 0,5 mg/kg a cada 12h", texto);
        Assert.Contains("/Title (Receituario - Thor)", texto);
    }

    [Fact]
    public void Pdf_UsaFonteQueTodoLeitorJaTem()
    {
        var pdf = _gerador.Renderizar("Documento", "conteudo");

        // Helvetica dispensa embutir arquivo de fonte — e o que permite gerar o PDF
        // sem trazer biblioteca nenhuma para o projeto (§11)
        Assert.Contains("/BaseFont /Helvetica", ComoTexto(pdf));
    }

    [Fact]
    public void Pdf_EscapaParenteseEmVezDeCorromperOArquivo()
    {
        var pdf = _gerador.Renderizar("Receita", "Dipirona (sodica) 25 mg/kg");

        var texto = ComoTexto(pdf);

        // Parentese delimita string no PDF: sem escapar, um nome de medicamento
        // com parentese corromperia o arquivo inteiro
        Assert.Contains("Dipirona \\(sodica\\) 25 mg/kg", texto);
    }

    [Fact]
    public void Pdf_ConteudoLongo_GeraMaisDeUmaPagina()
    {
        var longo = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"Linha {i} do prontuario."));

        var pdf = _gerador.Renderizar("Prontuario extenso", longo);

        var texto = ComoTexto(pdf);

        Assert.Contains("/Count 4", texto);
        Assert.Contains("Linha 200 do prontuario.", texto);
    }

    [Fact]
    public void Pdf_LinhaLonga_QuebraSemPartirPalavra()
    {
        var linha = string.Join(" ", Enumerable.Repeat("antiinflamatorio", 20));

        var pdf = _gerador.Renderizar("Receita", linha);

        var texto = ComoTexto(pdf);

        // Palavra cortada em documento clinico faz o leitor duvidar do resto
        Assert.DoesNotContain("antiinflamat\n", texto);
        Assert.Contains("antiinflamatorio", texto);
    }

    [Fact]
    public void Pdf_AcentoForaDoWinAnsi_NaoQuebraOArquivo()
    {
        var pdf = _gerador.Renderizar("Atestado", "Observação: coração acelerado — retorno em 7 dias.");

        var texto = ComoTexto(pdf);

        Assert.StartsWith("%PDF-1.4", texto);
        Assert.EndsWith("%%EOF\n", texto);
        Assert.Contains("cora", texto);
    }

    [Fact]
    public void Pdf_ConteudoVazio_NaoEAceito()
    {
        // PDF sem conteudo nao e documento: melhor falhar do que entregar folha em branco
        Assert.Throws<ArgumentException>(() => _gerador.Renderizar("Documento", "   "));
    }
}
