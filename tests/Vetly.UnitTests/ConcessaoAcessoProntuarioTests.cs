using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>Testes unitarios de dominio puro para ConcessaoAcessoProntuario (RN-083/085/087).</summary>
public class ConcessaoAcessoProntuarioTests
{
    private static ConcessaoAcessoProntuario CriarConcessao(DateTime concedidoEm, DateTime expiraEm) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), BaseAcesso.ConsentimentoRede, concedidoEm, expiraEm);

    [Fact]
    public void EstaAtiva_DentroDoPrazoENaoRevogada_RetornaTrue()
    {
        var agora = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var concessao = CriarConcessao(agora, agora.AddHours(24));

        Assert.True(concessao.EstaAtiva(agora.AddHours(10)));
    }

    [Fact]
    public void EstaAtiva_AposExpiraEm_RetornaFalse()
    {
        var agora = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var concessao = CriarConcessao(agora, agora.AddHours(24));

        Assert.False(concessao.EstaAtiva(agora.AddHours(25)));
    }

    [Fact]
    public void EstaAtiva_NoLimiteExatoDeExpiraEm_AindaAtiva()
    {
        var agora = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var concessao = CriarConcessao(agora, agora.AddHours(24));

        Assert.True(concessao.EstaAtiva(agora.AddHours(24)));
    }

    [Fact]
    public void Revogar_ConcessaoAindaDentroDoPrazo_ParaDeEstarAtivaSemApagarORegistro()
    {
        var agora = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var concessao = CriarConcessao(agora, agora.AddHours(24));

        concessao.Revogar();

        Assert.True(concessao.Revogada);
        Assert.False(concessao.EstaAtiva(agora.AddHours(1)));
        // O registro em si continua existindo com todos os seus dados — só passa a não valer mais.
        Assert.Equal(agora, concessao.ConcedidoEm);
    }
}
