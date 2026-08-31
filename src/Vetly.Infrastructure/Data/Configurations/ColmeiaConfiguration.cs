using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>Configuração EF Core para <see cref="AcessoColmeia"/> (TB_ACESSO_COLMEIA).</summary>
public class AcessoColmeiaConfiguration : IEntityTypeConfiguration<AcessoColmeia>
{
    public void Configure(EntityTypeBuilder<AcessoColmeia> builder)
    {
        builder.ToTable("TB_ACESSO_COLMEIA");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(a => a.AnimalId)
            .HasColumnType("CHAR(36)").HasColumnName("ANIMAL_ID").IsRequired();

        builder.Property(a => a.TutorId)
            .HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID").IsRequired();

        builder.Property(a => a.VeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("VETERINARIO_ID").IsRequired();

        builder.Property(a => a.EmpresaId)
            .HasColumnType("CHAR(36)").HasColumnName("EMPRESA_ID");

        builder.Property(a => a.Escopo)
            .HasConversion<int>().HasColumnName("ESCOPO").IsRequired();

        builder.Property(a => a.ConcedidoEm).HasColumnName("CONCEDIDO_EM").IsRequired();
        builder.Property(a => a.ExpiraEm).HasColumnName("EXPIRA_EM").IsRequired();
        builder.Property(a => a.RevogadoEm).HasColumnName("REVOGADO_EM");

        builder.Property(a => a.Motivo)
            .HasColumnType("VARCHAR2(300)").HasColumnName("MOTIVO");

        // A consulta quente é "este vet alcança este animal agora?", feita a cada
        // leitura de dado clínico fora do escopo próprio
        builder.HasIndex(a => new { a.AnimalId, a.VeterinarioId })
            .HasDatabaseName("IX_ACESSO_COLMEIA_ANIMAL_VET");

        builder.HasIndex(a => a.ExpiraEm).HasDatabaseName("IX_ACESSO_COLMEIA_EXPIRA_EM");
    }
}

/// <summary>
/// Configuração EF Core para <see cref="LogAcessoColmeia"/> (TB_LOG_ACESSO_COLMEIA).
///
/// Tabela append-only: nada aqui é atualizado nem removido pela aplicação.
/// </summary>
public class LogAcessoColmeiaConfiguration : IEntityTypeConfiguration<LogAcessoColmeia>
{
    public void Configure(EntityTypeBuilder<LogAcessoColmeia> builder)
    {
        builder.ToTable("TB_LOG_ACESSO_COLMEIA");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(l => l.AcessoColmeiaId)
            .HasColumnType("CHAR(36)").HasColumnName("ACESSO_COLMEIA_ID");

        builder.Property(l => l.AnimalId)
            .HasColumnType("CHAR(36)").HasColumnName("ANIMAL_ID").IsRequired();

        builder.Property(l => l.VeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("VETERINARIO_ID");

        builder.Property(l => l.Escopo)
            .HasConversion<int>().HasColumnName("ESCOPO").IsRequired();

        builder.Property(l => l.Rota)
            .HasColumnType("VARCHAR2(200)").HasColumnName("ROTA");

        builder.Property(l => l.Permitido)
            .HasColumnType("NUMBER(1)").HasColumnName("PERMITIDO").IsRequired();

        builder.Property(l => l.OcorridoEm).HasColumnName("OCORRIDO_EM").IsRequired();

        // O Responsável pergunta "quem leu o histórico do meu animal?": é por animal,
        // e em ordem de tempo
        builder.HasIndex(l => new { l.AnimalId, l.OcorridoEm })
            .HasDatabaseName("IX_LOG_COLMEIA_ANIMAL_OCORRIDO");

        builder.HasIndex(l => l.VeterinarioId).HasDatabaseName("IX_LOG_COLMEIA_VETERINARIO");
    }
}
