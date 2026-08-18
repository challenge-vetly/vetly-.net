using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Avaliacao"/>.
/// Mapeia para a tabela TB_AVALIACAO com convenções Oracle. Índice único em CONSULTA_ID
/// reforça no banco a regra "uma avaliação por consulta" (RN-076).
/// </summary>
public class AvaliacaoConfiguration : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> builder)
    {
        builder.ToTable("TB_AVALIACAO");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(a => a.ConsultaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("CONSULTA_ID")
            .IsRequired();

        builder.Property(a => a.ResponsavelId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("RESPONSAVEL_ID")
            .IsRequired();

        builder.Property(a => a.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        builder.Property(a => a.NotaGeral)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("NOTA_GERAL")
            .IsRequired();

        builder.Property(a => a.NotaAtendimento)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("NOTA_ATENDIMENTO");

        builder.Property(a => a.NotaPontualidade)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("NOTA_PONTUALIDADE");

        builder.Property(a => a.NotaEstrutura)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("NOTA_ESTRUTURA");

        builder.Property(a => a.NotaCustoBeneficio)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("NOTA_CUSTO_BENEFICIO");

        builder.Property(a => a.Comentario)
            .HasColumnType("VARCHAR2(2000)")
            .HasMaxLength(2000)
            .HasColumnName("COMENTARIO");

        builder.Property(a => a.Data)
            .HasColumnName("DATA")
            .IsRequired();

        builder.Property(a => a.StatusModeracao)
            .HasConversion<int>()
            .HasColumnName("STATUS_MODERACAO")
            .IsRequired();

        builder.Property(a => a.RespostaVeterinario)
            .HasColumnType("VARCHAR2(2000)")
            .HasMaxLength(2000)
            .HasColumnName("RESPOSTA_VETERINARIO");

        builder.Property(a => a.DataResposta)
            .HasColumnName("DATA_RESPOSTA");

        builder.Property(a => a.Invalidada)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("INVALIDADA");

        builder.HasIndex(a => a.ConsultaId)
            .IsUnique()
            .HasDatabaseName("IX_AVALIACAO_CONSULTA_UNICA");

        builder.HasIndex(a => a.VeterinarioId)
            .HasDatabaseName("IX_AVALIACAO_VETERINARIO");
    }
}
