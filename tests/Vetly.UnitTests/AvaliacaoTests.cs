using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.UnitTests;

/// <summary>Testes unitarios de dominio puro para Avaliacao (RN-076..082).</summary>
public class AvaliacaoTests
{
    private static readonly DateTime DataRealizada = new(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Avaliacao CriarAvaliacaoValida(DateTime? agora = null) =>
        Avaliacao.Criar(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            StatusConsulta.Realizada, DataRealizada,
            notaGeral: 5, notaAtendimento: 4, notaPontualidade: null, notaEstrutura: null, notaCustoBeneficio: null,
            comentario: "Ótimo atendimento", agora: agora ?? DataRealizada.AddHours(1));

    [Fact]
    public void Criar_ConsultaNaoRealizada_LancaDomainExceptionAVALIACAO002()
    {
        var ex = Assert.Throws<DomainException>(() => Avaliacao.Criar(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            StatusConsulta.Confirmada, null,
            5, null, null, null, null, null, DateTime.UtcNow));

        Assert.Equal("AVALIACAO-002", ex.Codigo);
    }

    [Fact]
    public void Criar_ForaDaJanelaDe7Dias_LancaDomainExceptionAVALIACAO001()
    {
        var ex = Assert.Throws<DomainException>(() => Avaliacao.Criar(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            StatusConsulta.Realizada, DataRealizada,
            5, null, null, null, null, null, DataRealizada.AddDays(7).AddSeconds(1)));

        Assert.Equal("AVALIACAO-001", ex.Codigo);
    }

    [Fact]
    public void Criar_NoLimiteExatoDaJanelaDe7Dias_CriaComSucesso()
    {
        var avaliacao = Avaliacao.Criar(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            StatusConsulta.Realizada, DataRealizada,
            5, null, null, null, null, null, DataRealizada.AddDays(7));

        Assert.Equal(5, avaliacao.NotaGeral);
        Assert.Equal(StatusModeracao.Publicada, avaliacao.StatusModeracao);
    }

    [Fact]
    public void Criar_NotaGeralForaDoIntervalo_LancaDomainExceptionAVALIACAO005()
    {
        var ex = Assert.Throws<DomainException>(() => Avaliacao.Criar(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            StatusConsulta.Realizada, DataRealizada,
            6, null, null, null, null, null, DataRealizada));

        Assert.Equal("AVALIACAO-005", ex.Codigo);
    }

    [Fact]
    public void Editar_DentroDe48h_AtualizaDados()
    {
        var avaliacao = CriarAvaliacaoValida();

        avaliacao.Editar(3, null, null, null, null, "Revi minha opinião", avaliacao.Data.AddHours(47));

        Assert.Equal(3, avaliacao.NotaGeral);
        Assert.Equal("Revi minha opinião", avaliacao.Comentario);
    }

    [Fact]
    public void Editar_Apos48hDaPublicacao_LancaDomainExceptionAVALIACAO003()
    {
        var avaliacao = CriarAvaliacaoValida();

        var ex = Assert.Throws<DomainException>(() =>
            avaliacao.Editar(3, null, null, null, null, null, avaliacao.Data.AddHours(48).AddMinutes(1)));

        Assert.Equal("AVALIACAO-003", ex.Codigo);
    }

    [Fact]
    public void Responder_PrimeiraResposta_RegistraComSucesso()
    {
        var avaliacao = CriarAvaliacaoValida();

        avaliacao.Responder("Obrigado pelo retorno!", avaliacao.Data.AddHours(2));

        Assert.Equal("Obrigado pelo retorno!", avaliacao.RespostaVeterinario);
        Assert.NotNull(avaliacao.DataResposta);
    }

    [Fact]
    public void Responder_SegundaResposta_LancaDomainExceptionAVALIACAO004()
    {
        var avaliacao = CriarAvaliacaoValida();
        avaliacao.Responder("Primeira resposta", avaliacao.Data.AddHours(1));

        var ex = Assert.Throws<DomainException>(
            () => avaliacao.Responder("Segunda resposta", avaliacao.Data.AddHours(2)));

        Assert.Equal("AVALIACAO-004", ex.Codigo);
    }

    [Fact]
    public void Moderar_OcultaComentario_NaoAlteraNotaGeral()
    {
        var avaliacao = CriarAvaliacaoValida();

        avaliacao.Moderar(StatusModeracao.OcultaPorModeracao);

        Assert.Equal(StatusModeracao.OcultaPorModeracao, avaliacao.StatusModeracao);
        Assert.Equal(5, avaliacao.NotaGeral); // RN-080: moderação nunca mexe na nota
    }

    [Fact]
    public void Invalidar_MarcaAvaliacaoComoInvalidada()
    {
        var avaliacao = CriarAvaliacaoValida();

        avaliacao.Invalidar();

        Assert.True(avaliacao.Invalidada);
    }
}
