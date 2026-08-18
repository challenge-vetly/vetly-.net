using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Empresa"/>.
/// Mapeia para a tabela TB_EMPRESA com convenções Oracle.
/// </summary>
public class EmpresaConfiguration : IEntityTypeConfiguration<Empresa>
{
    public void Configure(EntityTypeBuilder<Empresa> builder)
    {
        builder.ToTable("TB_EMPRESA");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(e => e.Nome)
            .HasColumnType("VARCHAR2(300)")
            .HasColumnName("NOME")
            .IsRequired();

        builder.Property(e => e.Tipo)
            .HasColumnType("VARCHAR2(100)")
            .HasColumnName("TIPO")
            .IsRequired();

        builder.Property(e => e.AdministradorId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ADMINISTRADOR_ID")
            .IsRequired();

        builder.Property(e => e.Ativa)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ATIVA");

        // NUMBER(8,2) comporta ate 999999.99 — folga generosa acima da faixa Enterprise (RN-092)
        builder.Property(e => e.FaixaEnterprise)
            .HasColumnType("NUMBER(8,2)")
            .HasColumnName("FAIXA_ENTERPRISE")
            .IsRequired();

        builder.HasIndex(e => e.AdministradorId).HasDatabaseName("IX_EMPRESA_ADMINISTRADOR");
    }
}
