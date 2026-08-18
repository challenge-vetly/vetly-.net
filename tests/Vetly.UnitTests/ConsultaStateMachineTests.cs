using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios de dominio puro para a maquina de estados de Consulta (RN-057..061).
/// Cobre transicoes validas e invalidas, expiracao do lock de checkout e remarcacao.
/// </summary>
public class ConsultaStateMachineTests
{
    private static Consulta CriarConsulta(TipoServico tipoServico = TipoServico.Consulta, ModalidadeAtendimento modalidade = ModalidadeAtendimento.Presencial) =>
        new(DateTime.UtcNow.AddDays(1), modalidade, tipoServico, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public void Ctor_NovaConsulta_NasceEmCheckoutSemLock()
    {
        var consulta = CriarConsulta();

        Assert.Equal(StatusConsulta.EmCheckout, consulta.Status);
        Assert.Null(consulta.LockCheckoutExpiraEm);
    }

    [Fact]
    public void IniciarCheckout_DefineLockDe10Minutos()
    {
        var consulta = CriarConsulta();
        var agora = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        consulta.IniciarCheckout(agora);

        Assert.Equal(agora.AddMinutes(10), consulta.LockCheckoutExpiraEm);
    }

    [Fact]
    public void ConfirmarPagamento_DentroDoLock_TransicionaParaConfirmada()
    {
        var consulta = CriarConsulta();
        var agora = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        consulta.IniciarCheckout(agora);

        consulta.ConfirmarPagamento(agora.AddMinutes(5));

        Assert.Equal(StatusConsulta.Confirmada, consulta.Status);
    }

    [Fact]
    public void ConfirmarPagamento_NoLimiteExatoDoLock_AindaConfirma()
    {
        var consulta = CriarConsulta();
        var agora = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        consulta.IniciarCheckout(agora);

        consulta.ConfirmarPagamento(agora.AddMinutes(10)); // exatamente no limite — ainda válido

        Assert.Equal(StatusConsulta.Confirmada, consulta.Status);
    }

    [Fact]
    public void ConfirmarPagamento_LockExpirado_LancaDomainExceptionCONSULTA011()
    {
        var consulta = CriarConsulta();
        var agora = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        consulta.IniciarCheckout(agora);

        var ex = Assert.Throws<DomainException>(
            () => consulta.ConfirmarPagamento(agora.AddMinutes(10).AddTicks(1)));

        Assert.Equal("CONSULTA-011", ex.Codigo);
    }

    [Fact]
    public void ConfirmarPagamento_SemIniciarCheckout_LancaDomainExceptionCONSULTA011()
    {
        var consulta = CriarConsulta(); // nunca chamou IniciarCheckout — LockCheckoutExpiraEm é nulo

        var ex = Assert.Throws<DomainException>(() => consulta.ConfirmarPagamento(DateTime.UtcNow));

        Assert.Equal("CONSULTA-011", ex.Codigo);
    }

    [Theory]
    [InlineData(StatusConsulta.Realizada)]
    [InlineData(StatusConsulta.Cancelada)]
    [InlineData(StatusConsulta.NoShowResponsavel)]
    [InlineData(StatusConsulta.NoShowVeterinario)]
    public void ConfirmarPagamento_APartirDeEstadoFinal_LancaDomainExceptionCONSULTA010(StatusConsulta estadoFinal)
    {
        var consulta = LevarA(estadoFinal);

        var ex = Assert.Throws<DomainException>(() => consulta.ConfirmarPagamento(DateTime.UtcNow));

        Assert.Equal("CONSULTA-010", ex.Codigo);
    }

    [Fact]
    public void Cancelar_APartirDeEmCheckout_TransicionaParaCancelada()
    {
        var consulta = CriarConsulta();

        consulta.Cancelar();

        Assert.Equal(StatusConsulta.Cancelada, consulta.Status);
    }

    [Fact]
    public void Cancelar_APartirDeConfirmada_TransicionaParaCancelada()
    {
        var consulta = LevarA(StatusConsulta.Confirmada);

        consulta.Cancelar();

        Assert.Equal(StatusConsulta.Cancelada, consulta.Status);
    }

    [Theory]
    [InlineData(StatusConsulta.Realizada)]
    [InlineData(StatusConsulta.Cancelada)]
    [InlineData(StatusConsulta.NoShowResponsavel)]
    [InlineData(StatusConsulta.NoShowVeterinario)]
    public void Cancelar_APartirDeEstadoFinal_LancaDomainExceptionCONSULTA010(StatusConsulta estadoFinal)
    {
        var consulta = LevarA(estadoFinal);

        var ex = Assert.Throws<DomainException>(consulta.Cancelar);

        Assert.Equal("CONSULTA-010", ex.Codigo);
    }

    [Fact]
    public void MarcarRealizada_APartirDeConfirmada_TransicionaERegistraDataRealizada()
    {
        var consulta = LevarA(StatusConsulta.Confirmada);
        var agora = DateTime.UtcNow;

        consulta.MarcarRealizada(agora);

        Assert.Equal(StatusConsulta.Realizada, consulta.Status);
        Assert.Equal(agora, consulta.DataRealizada);
    }

    [Fact]
    public void MarcarRealizada_APartirDeEmCheckout_LancaDomainExceptionCONSULTA010()
    {
        var consulta = CriarConsulta();

        var ex = Assert.Throws<DomainException>(() => consulta.MarcarRealizada(DateTime.UtcNow));

        Assert.Equal("CONSULTA-010", ex.Codigo);
    }

    [Fact]
    public void RegistrarNoShowResponsavel_APartirDeConfirmada_Transiciona()
    {
        var consulta = LevarA(StatusConsulta.Confirmada);

        consulta.RegistrarNoShowResponsavel();

        Assert.Equal(StatusConsulta.NoShowResponsavel, consulta.Status);
    }

    [Fact]
    public void RegistrarNoShowVeterinario_APartirDeEmCheckout_LancaDomainExceptionCONSULTA010()
    {
        var consulta = CriarConsulta();

        var ex = Assert.Throws<DomainException>(consulta.RegistrarNoShowVeterinario);

        Assert.Equal("CONSULTA-010", ex.Codigo);
    }

    [Fact]
    public void Reagendar_ConsultaConfirmada_AtualizaDataEIncrementaContador()
    {
        var consulta = LevarA(StatusConsulta.Confirmada);
        var novaData = DateTime.UtcNow.AddDays(5);

        consulta.Reagendar(novaData);
        consulta.Reagendar(novaData.AddDays(1));

        Assert.Equal(novaData.AddDays(1), consulta.DataHora);
        Assert.Equal(2, consulta.ContadorRemarcacoes);
    }

    [Fact]
    public void Reagendar_ConsultaCancelada_LancaDomainExceptionCONSULTA010()
    {
        var consulta = LevarA(StatusConsulta.Cancelada);

        var ex = Assert.Throws<DomainException>(() => consulta.Reagendar(DateTime.UtcNow.AddDays(5)));

        Assert.Equal("CONSULTA-010", ex.Codigo);
    }

    [Fact]
    public void PodeGerarDocumentos_EstadoFinalDefinidoEConfirmada_RetornaTrue()
    {
        var consulta = LevarA(StatusConsulta.Confirmada);
        consulta.DefinirDiagnosticoFinal("Diagnostico final de teste");

        Assert.True(consulta.PodeGerarDocumentos());
    }

    [Fact]
    public void PodeGerarDocumentos_SemEstadoFinalDefinido_RetornaFalse()
    {
        var consulta = LevarA(StatusConsulta.Confirmada);

        Assert.False(consulta.PodeGerarDocumentos());
    }

    [Fact]
    public void DefinirDiagnosticoFinal_MarcaEstadoFinalDefinido()
    {
        var consulta = CriarConsulta();

        consulta.DefinirDiagnosticoFinal("Otite externa");

        Assert.True(consulta.EstadoFinalDefinido);
        Assert.Equal("Otite externa", consulta.DiagnosticoFinal);
    }

    /// <summary>Utilitário de teste: avança a consulta pelas transições necessárias até o estado desejado.</summary>
    private static Consulta LevarA(StatusConsulta status)
    {
        var consulta = CriarConsulta();
        var agora = DateTime.UtcNow;
        consulta.IniciarCheckout(agora);

        if (status == StatusConsulta.EmCheckout)
            return consulta;

        consulta.ConfirmarPagamento(agora);

        return status switch
        {
            StatusConsulta.Confirmada => consulta,
            StatusConsulta.Realizada => Realizada(consulta, agora),
            StatusConsulta.Cancelada => Cancelada(consulta),
            StatusConsulta.NoShowResponsavel => NoShowResponsavel(consulta),
            StatusConsulta.NoShowVeterinario => NoShowVeterinario(consulta),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static Consulta Realizada(Consulta c, DateTime agora) { c.MarcarRealizada(agora); return c; }
    private static Consulta Cancelada(Consulta c) { c.Cancelar(); return c; }
    private static Consulta NoShowResponsavel(Consulta c) { c.RegistrarNoShowResponsavel(); return c; }
    private static Consulta NoShowVeterinario(Consulta c) { c.RegistrarNoShowVeterinario(); return c; }
}
