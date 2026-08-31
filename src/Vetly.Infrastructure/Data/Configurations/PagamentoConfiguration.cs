using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Pagamento"/>.
/// Mapeia para a tabela TB_PAGAMENTO com convenções Oracle.
/// </summary>
public class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("TB_PAGAMENTO");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(p => p.TutorId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("TUTOR_ID")
            .IsRequired();

        builder.Property(p => p.ConsultaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("CONSULTA_ID");

        builder.Property(p => p.InternacaoId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("INTERNACAO_ID");

        // NUMBER(18,2) para precisão monetária
        builder.Property(p => p.Valor)
            .HasColumnType("NUMBER(18,2)")
            .HasColumnName("VALOR")
            .IsRequired();

        builder.Property(p => p.MeioPagamento)
            .HasConversion<int>()
            .HasColumnName("MEIO_PAGAMENTO")
            .IsRequired();

        builder.Property(p => p.Momento)
            .HasColumnName("MOMENTO")
            .IsRequired();

        builder.Property(p => p.StatusPagamento)
            .HasConversion<int>()
            .HasColumnName("STATUS_PAGAMENTO")
            .IsRequired();

        // NUMBER(5,2) é suficiente para percentuais de 0 a 100 com 2 casas decimais
        builder.Property(p => p.PercentualSplit)
            .HasColumnType("NUMBER(5,2)")
            .HasColumnName("PERCENTUAL_SPLIT");

        builder.Property(p => p.ValorEstornado)
            .HasColumnType("NUMBER(18,2)")
            .HasColumnName("VALOR_ESTORNADO");

        builder.HasIndex(p => p.TutorId).HasDatabaseName("IX_PAGAMENTO_TUTOR");
        // ── Cobranca (RN-006/RN-071, §5.1) ───────────────────────────────────
        builder.Property(p => p.Tipo)
            .HasConversion<int>().HasColumnName("TIPO").IsRequired();

        builder.Property(p => p.ReferenciaExterna)
            .HasColumnType("VARCHAR2(100)").HasColumnName("REFERENCIA_EXTERNA");

        builder.Property(p => p.ChaveIdempotencia)
            .HasColumnType("VARCHAR2(100)").HasColumnName("CHAVE_IDEMPOTENCIA");

        builder.Property(p => p.Liquidado)
            .HasColumnType("NUMBER(1)").HasColumnName("LIQUIDADO").IsRequired();

        // O webhook chega com a referencia do provedor: e por ela que se acha o pagamento
        builder.HasIndex(p => p.ReferenciaExterna).HasDatabaseName("IX_PAGAMENTO_REFERENCIA");

        // ── Split por plano (RN-070/RN-071/RN-072) ───────────────────────────
        builder.Property(p => p.PlanoAplicado)
            .HasConversion<int?>().HasColumnName("PLANO");

        // Percentual 0-100, nao basis points (§2.3)
        builder.Property(p => p.TakeRate)
            .HasColumnType("NUMBER(5,2)").HasColumnName("TAKE_RATE");

        builder.Property(p => p.Comissao)
            .HasColumnType("NUMBER(18,2)").HasColumnName("COMISSAO");

        builder.Property(p => p.Repasse)
            .HasColumnType("NUMBER(18,2)").HasColumnName("REPASSE");

        builder.Property(p => p.DestinatarioRepasseId)
            .HasColumnType("CHAR(36)").HasColumnName("DESTINATARIO_REPASSE_ID");

        builder.HasIndex(p => p.ConsultaId).HasDatabaseName("IX_PAGAMENTO_CONSULTA");
    }
}
