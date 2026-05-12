using Microsoft.EntityFrameworkCore;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VetlyDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
