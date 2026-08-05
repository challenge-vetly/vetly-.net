using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.UnitTests;

/// <summary>Testes unitarios de dominio puro para a assinatura de Documento (RN-031/091).</summary>
public class DocumentoAssinaturaTests
{
    private static Documento CriarDocumento() =>
        new(TipoDocumento.ReceitaVeterinaria, "12345-SP", Guid.NewGuid());

    [Fact]
    public void Assinar_NomeValido_RegistraAssinaturaPorNomeDigitado()
    {
        var documento = CriarDocumento();
        var agora = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        documento.Assinar("Dr. João Silva", agora);

        Assert.True(documento.AssinadoDigitalmente);
        Assert.Equal("Dr. João Silva", documento.AssinaturaNomeDigitado);
        Assert.Equal(TipoAssinatura.NomeDigitado, documento.TipoAssinatura);
        Assert.Equal(agora, documento.DataAssinatura);
    }

    [Fact]
    public void Assinar_NomeVazio_LancaDomainExceptionDOCUMENTO001()
    {
        var documento = CriarDocumento();

        var ex = Assert.Throws<DomainException>(() => documento.Assinar("", DateTime.UtcNow));

        Assert.Equal("DOCUMENTO-001", ex.Codigo);
    }

    [Fact]
    public void Assinar_NomeSoComEspacos_LancaDomainExceptionDOCUMENTO001()
    {
        var documento = CriarDocumento();

        var ex = Assert.Throws<DomainException>(() => documento.Assinar("   ", DateTime.UtcNow));

        Assert.Equal("DOCUMENTO-001", ex.Codigo);
    }

    [Fact]
    public void Assinar_SempreDeixaDispensacaoDeControladosDesabilitada()
    {
        var documento = CriarDocumento();

        documento.Assinar("Dr. Vet", DateTime.UtcNow);

        Assert.False(documento.HabilitaDispensacaoControlados); // RN-091
    }
}
