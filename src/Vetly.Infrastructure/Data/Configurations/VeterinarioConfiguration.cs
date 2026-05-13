using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Veterinario"/>.
/// Mapeia para a tabela TB_VETERINARIO com convenções Oracle (VARCHAR2, NUMBER, CHAR).
/// </summary>
public class VeterinarioConfiguration : IEntityTypeConfiguration<Veterinario>
{
    public void Configure(EntityTypeBuilder<Veterinario> builder)
    {
        builder.ToTable("TB_VETERINARIO");

        // PK como CHAR(36) para armazenar Guid no formato padrão "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(v => v.Nome)
            .HasColumnType("VARCHAR2(200)")
            .HasColumnName("NOME")
            .IsRequired();

        builder.Property(v => v.UfAtuacao)
            .HasColumnType("CHAR(2)")
            .HasColumnName("UF_ATUACAO")
            .IsRequired();

        builder.Property(v => v.TitulacaoAcademica)
            .HasColumnType("VARCHAR2(300)")
            .HasColumnName("TITULACAO_ACADEMICA");

        // Enums persistidos como int para economia de espaço e índice eficiente no Oracle
        builder.Property(v => v.Persona)
            .HasConversion<int>()
            .HasColumnName("PERSONA")
            .IsRequired();

        builder.Property(v => v.Plano)
            .HasConversion<int>()
            .HasColumnName("PLANO")
            .IsRequired();

        // Oracle não tem tipo BOOLEAN nativo — NUMBER(1) com 0/1 é a convenção padrão
        builder.Property(v => v.Ativo)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ATIVO")
            .IsRequired();

        builder.Property(v => v.EmpresaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("EMPRESA_ID");

        // Listas persistidas como string delimitada por ponto-e-vírgula (VARCHAR2)
        // EF converte automaticamente via HasConversion; ponto-e-vírgula foi escolhido
        // por não ocorrer naturalmente em nomes de especialidades/espécies
        builder.Property(v => v.Especialidades)
            .HasConversion(
                v => string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasColumnType("VARCHAR2(1000)")
            .HasColumnName("ESPECIALIDADES");

        builder.Property(v => v.EspeciesAtendidas)
            .HasConversion(
                v => string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasColumnType("VARCHAR2(1000)")
            .HasColumnName("ESPECIES_ATENDIDAS");

        // Value object Crmv mapeado como owned entity — colunas ficam na mesma tabela
        builder.OwnsOne(v => v.Crmv, crmv =>
        {
            crmv.Property(c => c.Valor)
                .HasColumnName("CRMV")
                .HasColumnType("VARCHAR2(15)")
                .IsRequired();
        });

        // Índice no CRMV para validação rápida de unicidade (RN-011)
        builder.HasIndex("CRMV").HasDatabaseName("IX_VETERINARIO_CRMV").IsUnique();
        // Índice na UF para buscas por região (GET /api/veterinarios/regiao/{uf})
        builder.HasIndex(v => v.UfAtuacao).HasDatabaseName("IX_VETERINARIO_UF");
    }
}
