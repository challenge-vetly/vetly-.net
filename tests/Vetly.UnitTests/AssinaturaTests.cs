using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Quais documentos exigem assinatura para valer (RN-087, C-04).
///
/// A exigencia e sobre o documento que existe, nao sobre a consulta.
/// </summary>
public class AssinaturaTests
{
    private static Documento Documento(TipoDocumento tipo) =>
        new(tipo, "12345-SP", consultaId: Guid.NewGuid());

    [Theory]
    [InlineData(TipoDocumento.ReceitaVeterinaria)]
    [InlineData(TipoDocumento.Atestado)]
    public void DocumentoQueSaiDaPlataforma_ExigeAssinatura(TipoDocumento tipo)
    {
        // Receita e atestado afirmam algo em nome de um profissional habilitado: sem
        // assinatura, quem os recebe nao tem como saber de quem vieram
        Assert.True(Documento(tipo).ExigeAssinatura());
    }

    [Theory]
    [InlineData(TipoDocumento.Prontuario)]
    [InlineData(TipoDocumento.NotaFiscal)]
    public void RegistroInternoERecibo_NaoExigemAssinatura(TipoDocumento tipo)
    {
        // Nenhum dos dois faz afirmacao para fora, e exigir assinatura neles travaria
        // consultas que nao prescreveram nada (C-04)
        Assert.False(Documento(tipo).ExigeAssinatura());
    }

    [Fact]
    public void ReceitaSemAssinatura_FicaPendente()
    {
        Assert.True(Documento(TipoDocumento.ReceitaVeterinaria).PendenteDeAssinatura());
    }

    [Fact]
    public void ReceitaAssinada_DeixaDeEstarPendente()
    {
        var receita = Documento(TipoDocumento.ReceitaVeterinaria);

        receita.RegistrarAssinatura("NomeDigitado", "Assinado por Dra. Marina - CRMV 12345-SP");

        Assert.False(receita.PendenteDeAssinatura());
        Assert.Equal("NomeDigitado", receita.AssinaturaMetodo);
    }

    [Fact]
    public void ProntuarioSemAssinatura_NaoFicaPendente()
    {
        Assert.False(Documento(TipoDocumento.Prontuario).PendenteDeAssinatura());
    }

    [Fact]
    public void RegistrarAssinatura_SemMetodo_NaoEAceito()
    {
        var receita = Documento(TipoDocumento.ReceitaVeterinaria);

        // Assinatura sem metodo e assinatura que nao diz o quanto vale
        Assert.Throws<ArgumentException>(() => receita.RegistrarAssinatura("  ", "carimbo"));
    }
}
