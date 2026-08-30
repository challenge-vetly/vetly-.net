using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="Servico"/> (TB_SERVICO).
/// </summary>
public class ServicoConfiguration : IEntityTypeConfiguration<Servico>
{
    public void Configure(EntityTypeBuilder<Servico> builder)
    {
        builder.ToTable("TB_SERVICO");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(s => s.PrestadorId)
            .HasColumnType("CHAR(36)").HasColumnName("PRESTADOR_ID").IsRequired();

        builder.Property(s => s.Tipo)
            .HasConversion<int>().HasColumnName("TIPO").IsRequired();

        // Dinheiro como decimal com 2 casas — nunca centavos (§2.3)
        builder.Property(s => s.Valor)
            .HasColumnType("NUMBER(18,2)").HasColumnName("VALOR").IsRequired();

        builder.Property(s => s.AceitaPlanoPet)
            .HasColumnType("NUMBER(1)").HasColumnName("ACEITA_PLANO_PET").IsRequired();

        builder.Property(s => s.DuracaoMinutos)
            .HasColumnType("NUMBER(4)").HasColumnName("DURACAO_MINUTOS").IsRequired();

        builder.Property(s => s.Ativo)
            .HasColumnType("NUMBER(1)").HasColumnName("ATIVO").IsRequired();

        // Um prestador nao oferece o mesmo tipo de servico duas vezes
        builder.HasIndex(s => new { s.PrestadorId, s.Tipo })
            .HasDatabaseName("IX_SERVICO_PRESTADOR_TIPO").IsUnique();
    }
}
