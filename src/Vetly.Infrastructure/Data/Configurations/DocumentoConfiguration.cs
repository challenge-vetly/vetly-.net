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

        // NUMBER(1) para o flag de assinatura digital (RN-087)
        builder.Property(d => d.AssinadoDigitalmente)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ASSINADO_DIGITALMENTE");

        // Campos de auditoria de correção (RN-088)
        builder.Property(d => d.VersaoOriginalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VERSAO_ORIGINAL_ID");

        builder.Property(d => d.DataCorrecao)
            .HasColumnName("DATA_CORRECAO");

        builder.Property(d => d.CrmvSolicitanteCorrecao)
            .HasColumnType("VARCHAR2(15)")
            .HasColumnName("CRMV_SOLICITANTE_CORRECAO");

        // ── Conteúdo, assinatura e publicação (RN-083/087/090) ───────────────

        // CLOB: o conteúdo do documento não cabe em VARCHAR2(4000)
        builder.Property(d => d.Conteudo)
            .HasColumnType("CLOB")
            .HasColumnName("CONTEUDO");

        builder.Property(d => d.PdfMidiaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("PDF_MIDIA_ID");

        builder.Property(d => d.Subtipo)
            .HasConversion<int?>()
            .HasColumnName("SUBTIPO");

        builder.Property(d => d.AssinaturaMetodo)
            .HasColumnType("VARCHAR2(50)")
            .HasColumnName("ASSINATURA_METODO");

        builder.Property(d => d.AssinaturaCarimbo)
            .HasColumnType("VARCHAR2(300)")
            .HasColumnName("ASSINATURA_CARIMBO");

        builder.Property(d => d.PublicadoEm)
            .HasColumnName("PUBLICADO_EM");

        builder.Property(d => d.LidoEm)
            .HasColumnName("LIDO_EM");

        builder.HasIndex(d => d.ConsultaId).HasDatabaseName("IX_DOCUMENTO_CONSULTA");
        builder.HasIndex(d => d.InternacaoId).HasDatabaseName("IX_DOCUMENTO_INTERNACAO");
    }
}
