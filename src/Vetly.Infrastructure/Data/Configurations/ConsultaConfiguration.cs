using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Consulta"/>.
/// Mapeia para a tabela TB_CONSULTA com convenções Oracle.
/// </summary>
public class ConsultaConfiguration : IEntityTypeConfiguration<Consulta>
{
    public void Configure(EntityTypeBuilder<Consulta> builder)
    {
        builder.ToTable("TB_CONSULTA");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        // Indexado para consultas por período (GET /api/consultas?data=...)
        builder.Property(c => c.DataHora)
            .HasColumnName("DATA_HORA")
            .IsRequired();

        builder.Property(c => c.Modalidade)
            .HasConversion<int>()
            .HasColumnName("MODALIDADE")
            .IsRequired();

        builder.Property(c => c.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        builder.Property(c => c.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(c => c.TutorId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("TUTOR_ID")
            .IsRequired();

        builder.Property(c => c.DiagnosticoValidado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("DIAGNOSTICO_VALIDADO");

        builder.Property(c => c.ProtocoloValidado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("PROTOCOLO_VALIDADO");

        builder.Property(c => c.StatusPagamento)
            .HasConversion<int>()
            .HasColumnName("STATUS_PAGAMENTO")
            .IsRequired();

        builder.Property(c => c.Cancelada)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CANCELADA");

        builder.Property(c => c.Finalizada)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("FINALIZADA");

        // Índice composto para as buscas mais comuns: por veterinário + data
        builder.HasIndex(c => c.VeterinarioId).HasDatabaseName("IX_CONSULTA_VETERINARIO");
        builder.HasIndex(c => c.AnimalId).HasDatabaseName("IX_CONSULTA_ANIMAL");
        builder.HasIndex(c => c.DataHora).HasDatabaseName("IX_CONSULTA_DATA");
    }
}
