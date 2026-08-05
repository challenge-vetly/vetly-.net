using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="LogAcessoProntuario"/>.
/// Mapeia para a tabela TB_LOG_ACESSO_PRONTUARIO com convenções Oracle.
/// </summary>
public class LogAcessoProntuarioConfiguration : IEntityTypeConfiguration<LogAcessoProntuario>
{
    public void Configure(EntityTypeBuilder<LogAcessoProntuario> builder)
    {
        builder.ToTable("TB_LOG_ACESSO_PRONTUARIO");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(l => l.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(l => l.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        builder.Property(l => l.DataHora)
            .HasColumnName("DATA_HORA")
            .IsRequired();

        builder.Property(l => l.Contexto)
            .HasColumnType("VARCHAR2(500)")
            .HasColumnName("CONTEXTO")
            .IsRequired();

        builder.Property(l => l.BaseAcesso)
            .HasConversion<int>()
            .HasColumnName("BASE_ACESSO")
            .IsRequired();

        builder.HasIndex(l => l.AnimalId).HasDatabaseName("IX_LOG_ACESSO_PRONTUARIO_ANIMAL");
    }
}
