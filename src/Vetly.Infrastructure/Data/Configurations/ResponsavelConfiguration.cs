using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Responsavel"/>.
/// Mapeia para a tabela TB_RESPONSAVEL com convenções Oracle.
/// </summary>
public class ResponsavelConfiguration : IEntityTypeConfiguration<Responsavel>
{
    public void Configure(EntityTypeBuilder<Responsavel> builder)
    {
        builder.ToTable("TB_RESPONSAVEL");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(t => t.Nome)
            .HasColumnType("VARCHAR2(200)")
            .HasColumnName("NOME")
            .IsRequired();

        // VARCHAR2(254) segue o limite máximo de e-mail definido na RFC 5321
        builder.Property(t => t.Email)
            .HasColumnType("VARCHAR2(254)")
            .HasColumnName("EMAIL")
            .IsRequired();

        builder.Property(t => t.Telefone)
            .HasColumnType("VARCHAR2(20)")
            .HasColumnName("TELEFONE")
            .IsRequired();

        // Consentimento LGPD passa a ser modelado pela entidade ConsentimentoLgpd (v2,
        // ConsentimentoLgpdConfiguration) — os 3 booleanos antigos foram removidos daqui.

        builder.Property(t => t.Ativo)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ATIVO");

        // Estado de fidelidade e no-show (RN-064, RN-070/071) — v2
        builder.Property(t => t.TierFidelidade)
            .HasConversion<int>()
            .HasColumnName("TIER_FIDELIDADE")
            .IsRequired();

        builder.Property(t => t.SaldoPontos)
            .HasColumnType("NUMBER(10)")
            .HasColumnName("SALDO_PONTOS");

        builder.Property(t => t.SaldoCreditosVetly)
            .HasColumnType("NUMBER(10,2)")
            .HasColumnName("SALDO_CREDITOS_VETLY");

        builder.Property(t => t.ContadorNoShows)
            .HasColumnType("NUMBER(3)")
            .HasColumnName("CONTADOR_NO_SHOWS");

        builder.Property(t => t.DataUltimoNoShow)
            .HasColumnName("DATA_ULTIMO_NO_SHOW");

        builder.Property(t => t.BloqueadoDescontosAte)
            .HasColumnName("BLOQUEADO_DESCONTOS_ATE");

        // Índice no e-mail para buscas por e-mail e validação de unicidade
        builder.HasIndex(t => t.Email).HasDatabaseName("IX_RESPONSAVEL_EMAIL").IsUnique();
    }
}
