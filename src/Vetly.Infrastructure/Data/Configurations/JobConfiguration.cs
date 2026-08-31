using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>Configuração EF Core para <see cref="Job"/> (TB_JOB).</summary>
public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("TB_JOB");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(j => j.Tipo)
            .HasConversion<int>().HasColumnName("TIPO").IsRequired();

        builder.Property(j => j.Payload)
            .HasColumnType("VARCHAR2(2000)").HasColumnName("PAYLOAD");

        builder.Property(j => j.ExecutarEm).HasColumnName("EXECUTAR_EM").IsRequired();

        builder.Property(j => j.Tentativas)
            .HasColumnType("NUMBER(3)").HasColumnName("TENTATIVAS").IsRequired();

        builder.Property(j => j.Estado)
            .HasConversion<int>().HasColumnName("ESTADO").IsRequired();

        builder.Property(j => j.UltimoErro)
            .HasColumnType("VARCHAR2(1000)").HasColumnName("ULTIMO_ERRO");

        builder.Property(j => j.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();
        builder.Property(j => j.ConcluidoEm).HasColumnName("CONCLUIDO_EM");

        // A leitura do worker e sempre "o que esta pendente e ja venceu"
        builder.HasIndex(j => new { j.Estado, j.ExecutarEm }).HasDatabaseName("IX_JOB_FILA");
    }
}
