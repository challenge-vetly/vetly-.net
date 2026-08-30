using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="ItemListaEspera"/> (TB_LISTA_ESPERA).
/// </summary>
public class ItemListaEsperaConfiguration : IEntityTypeConfiguration<ItemListaEspera>
{
    public void Configure(EntityTypeBuilder<ItemListaEspera> builder)
    {
        builder.ToTable("TB_LISTA_ESPERA");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(i => i.TutorId)
            .HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID").IsRequired();

        builder.Property(i => i.AnimalId)
            .HasColumnType("CHAR(36)").HasColumnName("ANIMAL_ID").IsRequired();

        builder.Property(i => i.VeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("VETERINARIO_ID").IsRequired();

        builder.Property(i => i.Necessidade)
            .HasConversion<int>().HasColumnName("NECESSIDADE").IsRequired();

        builder.Property(i => i.Estado)
            .HasConversion<int>().HasColumnName("ESTADO").IsRequired();

        builder.Property(i => i.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();

        builder.Property(i => i.SlotOferecidoId)
            .HasColumnType("CHAR(36)").HasColumnName("SLOT_OFERECIDO_ID");

        builder.Property(i => i.PrioridadeAte).HasColumnName("PRIORIDADE_ATE");

        // A promocao busca quem esta aguardando na fila de um vet, na ordem de entrada
        builder.HasIndex(i => new { i.VeterinarioId, i.Estado, i.CriadoEm })
            .HasDatabaseName("IX_LISTA_ESPERA_FILA");

        builder.HasIndex(i => i.TutorId).HasDatabaseName("IX_LISTA_ESPERA_TUTOR");
    }
}
