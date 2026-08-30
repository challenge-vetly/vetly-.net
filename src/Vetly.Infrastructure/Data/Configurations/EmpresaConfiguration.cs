using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

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

        // ── Plano, política de cancelamento e endereço (RN-026/042/070/072) ──

        // NUMBER(5,2) aceita 0,00 a 100,00 — percentual, nao basis points (§2.3)
        builder.Property(e => e.PercentualRetencaoParcial)
            .HasColumnType("NUMBER(5,2)")
            .HasColumnName("PERCENTUAL_RETENCAO_PARCIAL")
            .IsRequired();

        builder.Property(e => e.Plano)
            .HasConversion<int>()
            .HasColumnName("PLANO")
            .IsRequired();

        builder.Property(e => e.FaixaEnterprise)
            .HasConversion<int?>()
            .HasColumnName("FAIXA_ENTERPRISE");

        // Endereço embutido, mesmo mapeamento usado em TB_VETERINARIO (RN-026)
        builder.OwnsOne(e => e.Endereco, endereco =>
        {
            endereco.Property(x => x.Cep)
                .HasColumnType("VARCHAR2(9)").HasColumnName("CEP").IsRequired();
            endereco.Property(x => x.Logradouro)
                .HasColumnType("VARCHAR2(200)").HasColumnName("LOGRADOURO");
            endereco.Property(x => x.Numero)
                .HasColumnType("VARCHAR2(20)").HasColumnName("NUMERO");
            endereco.Property(x => x.Complemento)
                .HasColumnType("VARCHAR2(100)").HasColumnName("COMPLEMENTO");
            endereco.Property(x => x.Bairro)
                .HasColumnType("VARCHAR2(150)").HasColumnName("BAIRRO");
            endereco.Property(x => x.Cidade)
                .HasColumnType("VARCHAR2(150)").HasColumnName("CIDADE");
            endereco.Property(x => x.Uf)
                .HasColumnType("CHAR(2)").HasColumnName("UF");
            endereco.Property(x => x.Latitude)
                .HasColumnType("NUMBER(9,6)").HasColumnName("LATITUDE");
            endereco.Property(x => x.Longitude)
                .HasColumnType("NUMBER(9,6)").HasColumnName("LONGITUDE");
            endereco.Property(x => x.CoordenadaRevisar)
                .HasColumnType("NUMBER(1)").HasColumnName("COORDENADA_REVISAR");

            endereco.HasIndex(x => new { x.Latitude, x.Longitude })
                .HasDatabaseName("IX_EMPRESA_COORDENADA");
        });

        builder.HasIndex(e => e.AdministradorId).HasDatabaseName("IX_EMPRESA_ADMINISTRADOR");
    }
}
