using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="RegistroOcultado"/>.
/// Mapeia para a tabela TB_REGISTRO_OCULTADO com convenções Oracle.
/// </summary>
public class RegistroOcultadoConfiguration : IEntityTypeConfiguration<RegistroOcultado>
{
    public void Configure(EntityTypeBuilder<RegistroOcultado> builder)
    {
        builder.ToTable("TB_REGISTRO_OCULTADO");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(r => r.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(r => r.ProntuarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("PRONTUARIO_ID")
            .IsRequired();

        builder.Property(r => r.DataOcultacao)
            .HasColumnName("DATA_OCULTACAO")
            .IsRequired();

        builder.HasIndex(r => new { r.AnimalId, r.ProntuarioId })
            .HasDatabaseName("IX_REGISTRO_OCULTADO_ANIMAL_PRONTUARIO")
            .IsUnique();
    }
}
