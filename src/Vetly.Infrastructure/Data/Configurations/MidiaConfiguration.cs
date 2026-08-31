using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>Configuração EF Core para <see cref="Midia"/> (TB_MIDIA).</summary>
public class MidiaConfiguration : IEntityTypeConfiguration<Midia>
{
    public void Configure(EntityTypeBuilder<Midia> builder)
    {
        builder.ToTable("TB_MIDIA");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(m => m.Tipo)
            .HasConversion<int>().HasColumnName("TIPO").IsRequired();

        builder.Property(m => m.ChaveStorage)
            .HasColumnType("VARCHAR2(300)").HasColumnName("CHAVE_STORAGE").IsRequired();

        builder.Property(m => m.ContentType)
            .HasColumnType("VARCHAR2(100)").HasColumnName("CONTENT_TYPE").IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<int>().HasColumnName("STATUS").IsRequired();

        builder.Property(m => m.TutorId).HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID");
        builder.Property(m => m.ConsultaId).HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ID");

        builder.Property(m => m.TamanhoBytes)
            .HasColumnType("NUMBER(19)").HasColumnName("TAMANHO_BYTES");

        builder.Property(m => m.CriadaEm).HasColumnName("CRIADA_EM").IsRequired();
        builder.Property(m => m.RetencaoAte).HasColumnName("RETENCAO_ATE");

        builder.HasIndex(m => m.ChaveStorage).HasDatabaseName("IX_MIDIA_CHAVE").IsUnique();

        // Midias de uma consulta: e como a captura de audio le seus segmentos
        builder.HasIndex(m => m.ConsultaId).HasDatabaseName("IX_MIDIA_CONSULTA");

        // Varredura de retencao vencida (P-06)
        builder.HasIndex(m => new { m.Status, m.RetencaoAte }).HasDatabaseName("IX_MIDIA_RETENCAO");
    }
}
