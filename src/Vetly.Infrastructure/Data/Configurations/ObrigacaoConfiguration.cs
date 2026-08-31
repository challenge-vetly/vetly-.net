using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>Configuração EF Core para <see cref="ObrigacaoPet"/> (TB_OBRIGACAO_PET).</summary>
public class ObrigacaoPetConfiguration : IEntityTypeConfiguration<ObrigacaoPet>
{
    public void Configure(EntityTypeBuilder<ObrigacaoPet> builder)
    {
        builder.ToTable("TB_OBRIGACAO_PET");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(o => o.AnimalId)
            .HasColumnType("CHAR(36)").HasColumnName("ANIMAL_ID").IsRequired();

        builder.Property(o => o.TutorId)
            .HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID").IsRequired();

        builder.Property(o => o.Tipo)
            .HasConversion<int>().HasColumnName("TIPO").IsRequired();

        builder.Property(o => o.Descricao)
            .HasColumnType("VARCHAR2(120)").HasColumnName("DESCRICAO").IsRequired();

        builder.Property(o => o.PeriodicidadeEmDias)
            .HasColumnType("NUMBER(5)").HasColumnName("PERIODICIDADE_DIAS").IsRequired();

        builder.Property(o => o.ProximoVencimento).HasColumnName("PROXIMO_VENCIMENTO").IsRequired();
        builder.Property(o => o.UltimoCumprimento).HasColumnName("ULTIMO_CUMPRIMENTO");

        builder.Property(o => o.UltimaConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("ULTIMA_CONSULTA_ID");

        builder.Property(o => o.RegistradaPorVeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("REGISTRADA_POR_VET_ID");

        builder.Property(o => o.DerivadaDaCarteira)
            .HasColumnType("NUMBER(1)").HasColumnName("DERIVADA_CARTEIRA").IsRequired();

        builder.Property(o => o.Arquivada)
            .HasColumnType("NUMBER(1)").HasColumnName("ARQUIVADA").IsRequired();

        builder.Property(o => o.CriadaEm).HasColumnName("CRIADA_EM").IsRequired();

        // O board carrega por animal; a rotina de lembretes varre por vencimento
        builder.HasIndex(o => new { o.AnimalId, o.Arquivada })
            .HasDatabaseName("IX_OBRIGACAO_ANIMAL");

        builder.HasIndex(o => o.ProximoVencimento).HasDatabaseName("IX_OBRIGACAO_VENCIMENTO");
    }
}
