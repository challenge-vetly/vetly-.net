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

        builder.Property(p => p.ResponsavelId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("RESPONSAVEL_ID")
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

        builder.HasIndex(p => p.ResponsavelId).HasDatabaseName("IX_PAGAMENTO_RESPONSAVEL");
        builder.HasIndex(p => p.ConsultaId).HasDatabaseName("IX_PAGAMENTO_CONSULTA");
    }
}
