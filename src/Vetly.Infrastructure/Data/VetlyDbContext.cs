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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VetlyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
