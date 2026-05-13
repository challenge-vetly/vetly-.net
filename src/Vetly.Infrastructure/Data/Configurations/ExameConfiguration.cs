using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Exame"/>.
/// Mapeia para a tabela TB_EXAME com convenções Oracle.
/// </summary>
public class ExameConfiguration : IEntityTypeConfiguration<Exame>
{
    public void Configure(EntityTypeBuilder<Exame> builder)
    {
        builder.ToTable("TB_EXAME");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(e => e.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(e => e.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        builder.Property(e => e.TipoSolicitacao)
            .HasColumnType("VARCHAR2(200)")
            .HasColumnName("TIPO_SOLICITACAO")
            .IsRequired();

        // CLOB para laudos extensos com imagens em base64 ou texto longo
        builder.Property(e => e.Resultado)
            .HasColumnType("CLOB")
            .HasColumnName("RESULTADO");

        builder.Property(e => e.LiberadoAoTutor)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("LIBERADO_AO_TUTOR");

        builder.Property(e => e.DataSolicitacao)
            .HasColumnName("DATA_SOLICITACAO")
            .IsRequired();

        builder.Property(e => e.DataResultado)
            .HasColumnName("DATA_RESULTADO");

        builder.HasIndex(e => e.AnimalId).HasDatabaseName("IX_EXAME_ANIMAL");
        builder.HasIndex(e => e.VeterinarioId).HasDatabaseName("IX_EXAME_VETERINARIO");
    }
}
