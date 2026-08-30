using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="Dispositivo"/>.
/// Mapeia para TB_DISPOSITIVO com convenções Oracle.
/// </summary>
public class DispositivoConfiguration : IEntityTypeConfiguration<Dispositivo>
{
    public void Configure(EntityTypeBuilder<Dispositivo> builder)
    {
        builder.ToTable("TB_DISPOSITIVO");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(d => d.TutorId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("TUTOR_ID")
            .IsRequired();

        builder.Property(d => d.PushToken)
            .HasColumnType("VARCHAR2(255)")
            .HasColumnName("PUSH_TOKEN")
            .IsRequired();

        builder.Property(d => d.Plataforma)
            .HasConversion<int>()
            .HasColumnName("PLATAFORMA")
            .IsRequired();

        builder.Property(d => d.RegistradoEm).HasColumnName("REGISTRADO_EM").IsRequired();
        builder.Property(d => d.UltimoUsoEm).HasColumnName("ULTIMO_USO_EM").IsRequired();

        builder.Property(d => d.Ativo)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ATIVO")
            .IsRequired();

        // Um push token pertence a um dispositivo só — reinstalar o app reaproveita o registro
        builder.HasIndex(d => d.PushToken).HasDatabaseName("IX_DISPOSITIVO_PUSH_TOKEN").IsUnique();

        // Disparar push para um Responsável percorre os dispositivos ativos dele
        builder.HasIndex(d => new { d.TutorId, d.Ativo }).HasDatabaseName("IX_DISPOSITIVO_TUTOR");
    }
}
