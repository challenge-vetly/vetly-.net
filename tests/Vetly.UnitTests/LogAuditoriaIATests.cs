using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios de dominio puro para LogAuditoriaIA (RN-098/099).
/// Cobre o ciclo pendente -> finalizado (uma unica vez) e a criacao ja completa
/// para artefatos automaticos (RN-097/099.1).
/// </summary>
public class LogAuditoriaIATests
{
    private static LogAuditoriaIA CriarLogPendente() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "12345-SP", "llama3.1",
            TipoSugestaoIA.Diagnostico, "[{\"hipotese\":\"Gastrite\"}]", DateTime.UtcNow);

    [Fact]
    public void Ctor_NovoLog_NascePendenteSemDecisao()
    {
        var log = CriarLogPendente();

        Assert.True(log.Pendente);
        Assert.Null(log.Decisao);
        Assert.Null(log.ConteudoFinal);
    }

    [Fact]
    public void RegistrarDecisao_Aprovar_FinalizaComConteudoIgualAoSugerido()
    {
        var log = CriarLogPendente();

        log.RegistrarDecisao(DecisaoVeterinario.Aprovar, log.ConteudoSugerido);

        Assert.False(log.Pendente);
        Assert.Equal(DecisaoVeterinario.Aprovar, log.Decisao);
        Assert.Equal(log.ConteudoSugerido, log.ConteudoFinal);
    }

    [Fact]
    public void RegistrarDecisao_Corrigir_FinalizaComTextoDoVeterinario()
    {
        var log = CriarLogPendente();

        log.RegistrarDecisao(DecisaoVeterinario.Corrigir, "Diagnostico reescrito pelo vet");

        Assert.Equal(DecisaoVeterinario.Corrigir, log.Decisao);
        Assert.Equal("Diagnostico reescrito pelo vet", log.ConteudoFinal);
        Assert.NotEqual(log.ConteudoSugerido, log.ConteudoFinal);
    }

    [Fact]
    public void RegistrarDecisao_NaoAprovar_FinalizaSemConteudoFinal()
    {
        var log = CriarLogPendente();

        log.RegistrarDecisao(DecisaoVeterinario.NaoAprovar, null);

        Assert.False(log.Pendente);
        Assert.Equal(DecisaoVeterinario.NaoAprovar, log.Decisao);
        Assert.Null(log.ConteudoFinal);
    }

    [Fact]
    public void RegistrarDecisao_ChamadoDuasVezes_LancaDomainExceptionIA002()
    {
        var log = CriarLogPendente();
        log.RegistrarDecisao(DecisaoVeterinario.Aprovar, log.ConteudoSugerido);

        var ex = Assert.Throws<DomainException>(
            () => log.RegistrarDecisao(DecisaoVeterinario.NaoAprovar, null));

        Assert.Equal("IA-002", ex.Codigo);
    }

    [Fact]
    public void RegistrarArtefatoAutomatico_CriaLogJaFinalizado()
    {
        var log = LogAuditoriaIA.RegistrarArtefatoAutomatico(
            Guid.NewGuid(), Guid.NewGuid(), "12345-SP", "formatacao-documento",
            TipoSugestaoIA.DocumentoGerado, "Receita: Amoxicilina 250mg", DateTime.UtcNow);

        Assert.False(log.Pendente);
        Assert.Equal("Receita: Amoxicilina 250mg", log.ConteudoFinal);
        Assert.Equal("Receita: Amoxicilina 250mg", log.ConteudoSugerido);
    }
}
