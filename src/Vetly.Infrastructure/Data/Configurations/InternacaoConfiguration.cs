using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Internacao"/>.
/// Mapeia para a tabela TB_INTERNACAO com convenções Oracle.
/// </summary>
public class InternacaoConfiguration : IEntityTypeConfiguration<Internacao>
{
    public void Configure(EntityTypeBuilder<Internacao> builder)
    {
        builder.ToTable("TB_INTERNACAO");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(i => i.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(i => i.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        // NUMBER(18,2) para valores monetários — evita arredondamentos de ponto flutuante
        builder.Property(i => i.ValorCaucao)
            .HasColumnType("NUMBER(18,2)")
            .HasColumnName("VALOR_CAUCAO")
            .IsRequired();

        builder.Property(i => i.ValorTotalApurado)
            .HasColumnType("NUMBER(18,2)")
            .HasColumnName("VALOR_TOTAL_APURADO");

        builder.Property(i => i.DataAbertura)
            .HasColumnName("DATA_ABERTURA")
            .IsRequired();

        builder.Property(i => i.DataAlta)
            .HasColumnName("DATA_ALTA");

        // CLOB para o JSON de procedimentos diários, que pode crescer ao longo dos dias
        builder.Property(i => i.ProcedimentosDiarios)
            .HasColumnType("CLOB")
            .HasColumnName("PROCEDIMENTOS_DIARIOS");

        builder.HasIndex(i => i.AnimalId).HasDatabaseName("IX_INTERNACAO_ANIMAL");
    }
}
