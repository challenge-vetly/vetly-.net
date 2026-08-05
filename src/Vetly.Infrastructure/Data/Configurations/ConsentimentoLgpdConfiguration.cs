using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="ConsentimentoLgpd"/>.
/// Mapeia para a tabela TB_CONSENTIMENTO_LGPD com convenções Oracle.
/// </summary>
public class ConsentimentoLgpdConfiguration : IEntityTypeConfiguration<ConsentimentoLgpd>
{
    public void Configure(EntityTypeBuilder<ConsentimentoLgpd> builder)
    {
        builder.ToTable("TB_CONSENTIMENTO_LGPD");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(c => c.ResponsavelId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("RESPONSAVEL_ID")
            .IsRequired();

        builder.Property(c => c.Finalidade)
            .HasConversion<int>()
            .HasColumnName("FINALIDADE")
            .IsRequired();

        builder.Property(c => c.Concedido)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CONCEDIDO");

        builder.Property(c => c.DataConcessao)
            .HasColumnName("DATA_CONCESSAO")
            .IsRequired();

        builder.Property(c => c.DataRevogacao)
            .HasColumnName("DATA_REVOGACAO");

        // Acelera tanto o histórico completo (ObterPorResponsavelAsync) quanto a busca
        // do registro ativo por finalidade (ObterAtivoAsync).
        builder.HasIndex(c => new { c.ResponsavelId, c.Finalidade })
            .HasDatabaseName("IX_CONSENTIMENTO_RESPONSAVEL_FINALIDADE");
    }
}
