using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="AgendaConfig"/> (TB_AGENDA_CONFIG).
/// </summary>
public class AgendaConfigConfiguration : IEntityTypeConfiguration<AgendaConfig>
{
    public void Configure(EntityTypeBuilder<AgendaConfig> builder)
    {
        builder.ToTable("TB_AGENDA_CONFIG");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(a => a.VeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("VETERINARIO_ID").IsRequired();

        // Flags de dias da semana num unico NUMBER, em vez de sete colunas
        builder.Property(a => a.Dias)
            .HasConversion<int>().HasColumnName("DIAS").IsRequired();

        // Horario em minutos desde a meia-noite: inteiro simples, sem depender de
        // suporte a TimeOnly no provider Oracle.
        builder.Property(a => a.InicioEmMinutos)
            .HasColumnType("NUMBER(4)").HasColumnName("INICIO_EM_MINUTOS").IsRequired();

        builder.Property(a => a.FimEmMinutos)
            .HasColumnType("NUMBER(4)").HasColumnName("FIM_EM_MINUTOS").IsRequired();

        builder.Property(a => a.DuracaoMinutos)
            .HasColumnType("NUMBER(4)").HasColumnName("DURACAO_MINUTOS").IsRequired();

        builder.Property(a => a.IntervaloMinutos)
            .HasColumnType("NUMBER(4)").HasColumnName("INTERVALO_MINUTOS").IsRequired();

        builder.Property(a => a.AtualizadaEm).HasColumnName("ATUALIZADA_EM").IsRequired();

        // Um veterinario tem uma configuracao de agenda
        builder.HasIndex(a => a.VeterinarioId).HasDatabaseName("IX_AGENDA_CONFIG_VETERINARIO").IsUnique();
    }
}
