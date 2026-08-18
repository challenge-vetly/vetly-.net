using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="PontosFidelidade"/>.
/// Mapeia para a tabela TB_PONTOS_FIDELIDADE com convenções Oracle.
/// </summary>
public class PontosFidelidadeConfiguration : IEntityTypeConfiguration<PontosFidelidade>
{
    public void Configure(EntityTypeBuilder<PontosFidelidade> builder)
    {
        builder.ToTable("TB_PONTOS_FIDELIDADE");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(p => p.ResponsavelId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("RESPONSAVEL_ID")
            .IsRequired();

        builder.Property(p => p.ConsultaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("CONSULTA_ID")
            .IsRequired();

        builder.Property(p => p.Origem)
            .HasConversion<int>()
            .HasColumnName("ORIGEM")
            .IsRequired();

        builder.Property(p => p.Pontos)
            .HasColumnType("NUMBER(10)")
            .HasColumnName("PONTOS")
            .IsRequired();

        builder.Property(p => p.Data)
            .HasColumnName("DATA")
            .IsRequired();

        builder.Property(p => p.ExpiraEm)
            .HasColumnName("EXPIRA_EM")
            .IsRequired();

        builder.Property(p => p.Estornado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ESTORNADO");

        builder.HasIndex(p => p.ResponsavelId)
            .HasDatabaseName("IX_PONTOS_FIDELIDADE_RESPONSAVEL");

        builder.HasIndex(p => p.ConsultaId)
            .HasDatabaseName("IX_PONTOS_FIDELIDADE_CONSULTA");
    }
}
