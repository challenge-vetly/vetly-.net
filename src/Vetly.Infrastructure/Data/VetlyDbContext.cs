using Microsoft.EntityFrameworkCore;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data;
// esse arquivo é importante para o EF Core, pois é onde definimos os DbSets e as configurações de mapeamento das entidades para as tabelas do banco de dados. 
//Ele serve como a ponte entre o modelo de domínio e a camada de persistência, permitindo que o EF Core saiba como materializar as entidades a partir dos dados armazenados no Oracle.
public class VetlyDbContext : DbContext
{
    public VetlyDbContext(DbContextOptions<VetlyDbContext> options) : base(options) { }

    public DbSet<Veterinario> Veterinarios => Set<Veterinario>();
    public DbSet<Animal> Animais => Set<Animal>();
    public DbSet<Tutor> Tutores => Set<Tutor>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<Prontuario> Prontuarios => Set<Prontuario>();
    public DbSet<Exame> Exames => Set<Exame>();
    public DbSet<Internacao> Internacoes => Set<Internacao>();
    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<LembreteAgendado> Lembretes => Set<LembreteAgendado>();

    /// <summary>Refresh tokens rotativos emitidos no login (§2.2).</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Dispositivos do Responsável registrados para push (RN-007/RN-092).</summary>
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();

    /// <summary>Configuração de agenda dos veterinários (RN-034).</summary>
    public DbSet<AgendaConfig> AgendaConfigs => Set<AgendaConfig>();

    /// <summary>Horários materializados da agenda (RN-034/RN-035).</summary>
    public DbSet<Slot> Slots => Set<Slot>();

    /// <summary>Serviços oferecidos pelos prestadores (RN-032/RN-074).</summary>
    public DbSet<Servico> Servicos => Set<Servico>();

    /// <summary>Tabela de apoio da geocodificação simulada (RN-026, §5.6).</summary>
    public DbSet<CepCoordenada> CepCoordenadas => Set<CepCoordenada>();

    /// <summary>Lista de espera por horário (RN-004/RN-037).</summary>
    public DbSet<ItemListaEspera> ListaDeEspera => Set<ItemListaEspera>();

    /// <summary>Respostas guardadas das requisições idempotentes (§2.5).</summary>
    public DbSet<RegistroIdempotencia> RegistrosDeIdempotencia => Set<RegistroIdempotencia>();

    /// <summary>Fila de trabalhos de negócio executados fora da requisição (§11).</summary>
    public DbSet<Job> Jobs => Set<Job>();

    /// <summary>Arquivos guardados no storage de objetos (§2.6).</summary>
    public DbSet<Midia> Midias => Set<Midia>();

    /// <summary>Janelas de captura das consultas (RN-008/RN-079).</summary>
    public DbSet<SessaoCaptura> SessoesDeCaptura => Set<SessaoCaptura>();

    /// <summary>Segmentos de áudio capturados (RN-009).</summary>
    public DbSet<SegmentoAudio> SegmentosDeAudio => Set<SegmentoAudio>();

    /// <summary>Texto produzido pelo motor de transcrição (RN-009).</summary>
    public DbSet<Transcricao> Transcricoes => Set<Transcricao>();

    /// <summary>Prontuários estruturados pela IA, ainda sem decisão do vet (RN-080).</summary>
    public DbSet<RascunhoIa> RascunhosDeIa => Set<RascunhoIa>();

    /// <summary>Trilha append-only das decisões sobre conteúdo de IA (RN-082).</summary>
    public DbSet<LogAuditoriaIa> LogsDeAuditoriaIa => Set<LogAuditoriaIa>();

    /// <summary>Caixa de entrada de notificações do Responsável (RN-092).</summary>
    public DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    /// <summary>Avaliações dos atendimentos (RN-055/RN-057).</summary>
    public DbSet<Avaliacao> Avaliacoes => Set<Avaliacao>();

    /// <summary>Extrato de pontos de fidelidade (RN-047 a RN-052).</summary>
    public DbSet<MovimentoDePontos> MovimentosDePontos => Set<MovimentoDePontos>();

    /// <summary>Cupons emitidos no resgate de pontos (RN-053/RN-054).</summary>
    public DbSet<CupomResgate> CuponsDeResgate => Set<CupomResgate>();

    /// <summary>Obrigações recorrentes de cuidado do animal (RN-045/RN-046).</summary>
    public DbSet<ObrigacaoPet> ObrigacoesDoPet => Set<ObrigacaoPet>();

    /// <summary>Autorizações do Responsável na colmeia (RN-090).</summary>
    public DbSet<AcessoColmeia> AcessosDaColmeia => Set<AcessoColmeia>();

    /// <summary>Trilha append-only dos acessos feitos pela colmeia (RN-090).</summary>
    public DbSet<LogAcessoColmeia> LogsDeAcessoDaColmeia => Set<LogAcessoColmeia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VetlyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
