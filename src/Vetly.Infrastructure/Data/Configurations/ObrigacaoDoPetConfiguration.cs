using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="ObrigacaoDoPet"/>.
/// Mapeia para a tabela TB_OBRIGACAO_PET com convenções Oracle.
/// </summary>
public class ObrigacaoDoPetConfiguration : IEntityTypeConfiguration<ObrigacaoDoPet>
{
    public void Configure(EntityTypeBuilder<ObrigacaoDoPet> builder)
    {
        builder.ToTable("TB_OBRIGACAO_PET");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(o => o.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(o => o.Tipo)
            .HasConversion<int>()
            .HasColumnName("TIPO")
            .IsRequired();

        builder.Property(o => o.DataLimite)
            .HasColumnName("DATA_LIMITE")
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<int>()
            .HasColumnName("STATUS")
            .IsRequired();

        builder.Property(o => o.ConsultaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("CONSULTA_ID");

        builder.Property(o => o.DataCumprimento)
            .HasColumnName("DATA_CUMPRIMENTO");

        builder.HasIndex(o => o.AnimalId)
            .HasDatabaseName("IX_OBRIGACAO_PET_ANIMAL");
    }
}
