using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="LogAuditoriaIA"/>.
/// Mapeia para a tabela TB_LOG_AUDITORIA_IA com convenções Oracle.
/// </summary>
public class LogAuditoriaIAConfiguration : IEntityTypeConfiguration<LogAuditoriaIA>
{
    public void Configure(EntityTypeBuilder<LogAuditoriaIA> builder)
    {
        builder.ToTable("TB_LOG_AUDITORIA_IA");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(l => l.ConsultaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("CONSULTA_ID")
            .IsRequired();

        builder.Property(l => l.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        builder.Property(l => l.Crmv)
            .HasColumnType("VARCHAR2(15)")
            .HasColumnName("CRMV")
            .IsRequired();

        builder.Property(l => l.Timestamp)
            .HasColumnName("TIMESTAMP")
            .IsRequired();

        builder.Property(l => l.VersaoModelo)
            .HasColumnType("VARCHAR2(100)")
            .HasColumnName("VERSAO_MODELO")
            .IsRequired();

        builder.Property(l => l.TipoSugestao)
            .HasConversion<int>()
            .HasColumnName("TIPO_SUGESTAO")
            .IsRequired();

        // CLOB para acomodar respostas de IA extensas (hipóteses, protocolo)
        builder.Property(l => l.ConteudoSugerido)
            .HasColumnType("CLOB")
            .HasColumnName("CONTEUDO_SUGERIDO")
            .IsRequired();

        builder.Property(l => l.Decisao)
            .HasConversion<int?>()
            .HasColumnName("DECISAO");

        builder.Property(l => l.ConteudoFinal)
            .HasColumnType("CLOB")
            .HasColumnName("CONTEUDO_FINAL");

        builder.HasIndex(l => l.ConsultaId).HasDatabaseName("IX_LOG_AUDITORIA_IA_CONSULTA");
    }
}
