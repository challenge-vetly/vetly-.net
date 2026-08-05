using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Animal"/>.
/// Mapeia para a tabela TB_ANIMAL com convenções Oracle.
/// </summary>
public class AnimalConfiguration : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("TB_ANIMAL");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(a => a.Nome)
            .HasColumnType("VARCHAR2(200)")
            .HasColumnName("NOME")
            .IsRequired();

        builder.Property(a => a.Especie)
            .HasColumnType("VARCHAR2(100)")
            .HasColumnName("ESPECIE")
            .IsRequired();

        builder.Property(a => a.Raca)
            .HasColumnType("VARCHAR2(100)")
            .HasColumnName("RACA")
            .IsRequired();

        builder.Property(a => a.DataNascimento)
            .HasColumnName("DATA_NASCIMENTO")
            .IsRequired();

        builder.Property(a => a.ResponsavelId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("RESPONSAVEL_ID")
            .IsRequired();

        // NUMBER(1) para boolean — padrão Oracle
        builder.Property(a => a.Ativo)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ATIVO")
            .IsRequired();

        // Alertas armazenados como string delimitada; ";" é sentinel para lista vazia
        // (Oracle trata "" como NULL; Split com RemoveEmptyEntries lê ";" de volta como [])
        builder.Property(a => a.AlertasAtivos)
            .HasConversion(
                v => v.Count == 0 ? ";" : string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasColumnType("VARCHAR2(2000)")
            .HasColumnName("ALERTAS_ATIVOS");

        // Índice para buscar todos os animais de um responsavel eficientemente
        builder.HasIndex(a => a.ResponsavelId).HasDatabaseName("IX_ANIMAL_RESPONSAVEL");
    }
}
