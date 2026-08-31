using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Ciclo de vida de um trabalho da fila (§11). Os handlers, que vivem na
/// Infrastructure, sao testados no projeto de integracao.
/// </summary>
public class JobTests
{
    [Fact]
    public void Job_SemAtraso_JaPodeRodar()
    {
        var job = new Job(TipoJob.PromoverListaEspera, "payload");

        Assert.True(job.Elegivel(DateTime.UtcNow));
        Assert.Equal(EstadoJob.Pendente, job.Estado);
    }

    [Fact]
    public void Job_ComAtraso_SoRodaDepois()
    {
        var job = new Job(TipoJob.ConfirmarPagamentoSimulado, "payload", TimeSpan.FromSeconds(2));

        Assert.False(job.Elegivel(DateTime.UtcNow));
        Assert.True(job.Elegivel(DateTime.UtcNow.AddSeconds(3)));
    }

    [Fact]
    public void Concluir_TiraODaFila()
    {
        var job = new Job(TipoJob.PromoverListaEspera);

        job.Concluir();

        Assert.Equal(EstadoJob.Concluido, job.Estado);
        Assert.False(job.Elegivel(DateTime.UtcNow.AddDays(1)));
        Assert.NotNull(job.ConcluidoEm);
    }

    [Fact]
    public void RegistrarFalha_ReagendaComEsperaCrescente()
    {
        var job = new Job(TipoJob.PromoverListaEspera);
        var agora = DateTime.UtcNow;

        job.RegistrarFalha("primeira falha", agora);
        var primeiraEspera = job.ExecutarEm - agora;

        job.RegistrarFalha("segunda falha", agora);
        var segundaEspera = job.ExecutarEm - agora;

        // Insistir no mesmo ritmo em algo que acabou de falhar so gasta o worker
        Assert.True(segundaEspera > primeiraEspera);
        Assert.Equal(EstadoJob.Pendente, job.Estado);
    }

    [Fact]
    public void RegistrarFalha_TresVezes_Desiste()
    {
        var job = new Job(TipoJob.PromoverListaEspera);
        var agora = DateTime.UtcNow;

        for (var i = 0; i < Job.MaximoDeTentativas; i++)
            job.RegistrarFalha("falha", agora);

        // Repetir para sempre um job que sempre falha consumiria o worker; ele fica
        // registrado como falho, para inspecao, em vez de sumir
        Assert.Equal(EstadoJob.Falhou, job.Estado);
        Assert.False(job.Elegivel(agora.AddDays(1)));
        Assert.Equal("falha", job.UltimoErro);
    }

    [Fact]
    public void RegistrarFalha_ErroMuitoLongo_ETruncado()
    {
        var job = new Job(TipoJob.PromoverListaEspera);

        job.RegistrarFalha(new string('x', 5000), DateTime.UtcNow);

        Assert.Equal(1000, job.UltimoErro!.Length);
    }
}
