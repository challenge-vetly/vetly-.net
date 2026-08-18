using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="LembreteAgendado"/>.
/// Mapeia para a tabela TB_LEMBRETE com convenções Oracle
/// (NUMBER(1) para booleans, CHAR(36) para Guids).
/// </summary>
public class LembreteAgendadoConfiguration : IEntityTypeConfiguration<LembreteAgendado>
{
    public void Configure(EntityTypeBuilder<LembreteAgendado> builder)
    {
        builder.ToTable("TB_LEMBRETE");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(l => l.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(l => l.ResponsavelId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("RESPONSAVEL_ID")
            .IsRequired();

        builder.Property(l => l.Tipo)
            .HasConversion<int>()
            .HasColumnName("TIPO")
            .IsRequired();

        builder.Property(l => l.DataEvento)
            .HasColumnName("DATA_EVENTO")
            .IsRequired();

        builder.Property(l => l.TentativasRealizadas)
            .HasColumnName("TENTATIVAS_REALIZADAS");

        // Oracle não suporta BOOLEAN nativo até 23c — usamos NUMBER(1) com 0/1
        builder.Property(l => l.ResponsavelRespondeu)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("RESPONSAVEL_RESPONDEU");

        builder.Property(l => l.AlertaEnviadoClinica)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ALERTA_ENVIADO_CLINICA");

        builder.HasIndex(l => l.ResponsavelId).HasDatabaseName("IX_LEMBRETE_RESPONSAVEL");
        builder.HasIndex(l => l.AnimalId).HasDatabaseName("IX_LEMBRETE_ANIMAL");
    }
}
