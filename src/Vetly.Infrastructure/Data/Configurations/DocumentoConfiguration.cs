using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Documento"/>.
/// Mapeia para a tabela TB_DOCUMENTO com convenções Oracle.
/// </summary>
public class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("TB_DOCUMENTO");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        // Nullable — documento pode estar vinculado a consulta OU internação, nunca ambos
        builder.Property(d => d.ConsultaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("CONSULTA_ID");

        builder.Property(d => d.InternacaoId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("INTERNACAO_ID");

        builder.Property(d => d.TipoDocumento)
            .HasConversion<int>()
            .HasColumnName("TIPO_DOCUMENTO")
            .IsRequired();

        builder.Property(d => d.Versao)
            .HasColumnName("VERSAO")
            .IsRequired();

        builder.Property(d => d.DataGeracao)
            .HasColumnName("DATA_GERACAO")
            .IsRequired();

        builder.Property(d => d.CrmvSignatario)
            .HasColumnType("VARCHAR2(15)")
            .HasColumnName("CRMV_SIGNATARIO")
            .IsRequired();

        // NUMBER(1) para o flag de assinatura digital (RN-031)
        builder.Property(d => d.AssinadoDigitalmente)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ASSINADO_DIGITALMENTE");

        builder.HasIndex(d => d.ConsultaId).HasDatabaseName("IX_DOCUMENTO_CONSULTA");
        builder.HasIndex(d => d.InternacaoId).HasDatabaseName("IX_DOCUMENTO_INTERNACAO");
    }
}
