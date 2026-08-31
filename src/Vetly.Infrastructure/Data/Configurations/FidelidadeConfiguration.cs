using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="MovimentoDePontos"/> (TB_MOVIMENTO_PONTOS).
///
/// Tabela append-only: nada aqui é atualizado nem removido pela aplicação.
/// </summary>
public class MovimentoDePontosConfiguration : IEntityTypeConfiguration<MovimentoDePontos>
{
    public void Configure(EntityTypeBuilder<MovimentoDePontos> builder)
    {
        builder.ToTable("TB_MOVIMENTO_PONTOS");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(m => m.TutorId)
            .HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID").IsRequired();

        builder.Property(m => m.Tipo)
            .HasConversion<int>().HasColumnName("TIPO").IsRequired();

        // Assinado: negativo em débito e expiração, para que o saldo seja a soma da coluna
        builder.Property(m => m.Pontos)
            .HasColumnType("NUMBER(10)").HasColumnName("PONTOS").IsRequired();

        builder.Property(m => m.ConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ID");

        builder.Property(m => m.PagamentoId)
            .HasColumnType("CHAR(36)").HasColumnName("PAGAMENTO_ID");

        builder.Property(m => m.ValorEmReais)
            .HasColumnType("NUMBER(18,2)").HasColumnName("VALOR_EM_REAIS");

        builder.Property(m => m.ExpiraEm).HasColumnName("EXPIRA_EM");

        builder.Property(m => m.MovimentoOrigemId)
            .HasColumnType("CHAR(36)").HasColumnName("MOVIMENTO_ORIGEM_ID");

        builder.Property(m => m.Descricao)
            .HasColumnType("VARCHAR2(200)").HasColumnName("DESCRICAO");

        builder.Property(m => m.OcorridoEm).HasColumnName("OCORRIDO_EM").IsRequired();

        // O saldo é somado por Responsável; a rotina de expiração varre por vencimento
        builder.HasIndex(m => new { m.TutorId, m.OcorridoEm })
            .HasDatabaseName("IX_PONTOS_TUTOR_OCORRIDO");

        builder.HasIndex(m => m.ExpiraEm).HasDatabaseName("IX_PONTOS_EXPIRA_EM");

        builder.HasIndex(m => m.ConsultaId).HasDatabaseName("IX_PONTOS_CONSULTA");
    }
}
