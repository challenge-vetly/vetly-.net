using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="Slot"/> (TB_SLOT).
/// </summary>
public class SlotConfiguration : IEntityTypeConfiguration<Slot>
{
    public void Configure(EntityTypeBuilder<Slot> builder)
    {
        builder.ToTable("TB_SLOT");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(s => s.VeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("VETERINARIO_ID").IsRequired();

        builder.Property(s => s.Inicio).HasColumnName("INICIO").IsRequired();
        builder.Property(s => s.Fim).HasColumnName("FIM").IsRequired();

        // ESTADO e LOCK_CONSULTA_ID sao tokens de concorrencia: o UPDATE do EF passa a
        // levar "WHERE ESTADO = <lido> AND LOCK_CONSULTA_ID = <lido>" na clausula.
        //
        // Sem isso, duas requisicoes que leem o mesmo slot Livre no mesmo milissegundo
        // escrevem EmCheckout as duas, e a ultima ganha: dois animais no mesmo horario,
        // que e exatamente o que a RN-035 existe para impedir. Com o token, a segunda
        // gravacao afeta zero linhas e o repositorio a traduz em 409.
        //
        // Usar as colunas que ja existem, em vez de acrescentar VERSAO, evita migracao
        // e backfill: o estado do horario ja e a informacao que precisa ser conferida.
        builder.Property(s => s.Estado)
            .HasConversion<int>().HasColumnName("ESTADO").IsRequired()
            .IsConcurrencyToken();

        builder.Property(s => s.LockAte).HasColumnName("LOCK_ATE");

        builder.Property(s => s.LockConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("LOCK_CONSULTA_ID")
            .IsConcurrencyToken();

        // O mesmo veterinario nao pode ter dois horarios comecando no mesmo instante:
        // materializar a agenda duas vezes nao pode duplicar a disponibilidade.
        builder.HasIndex(s => new { s.VeterinarioId, s.Inicio })
            .HasDatabaseName("IX_SLOT_VETERINARIO_INICIO").IsUnique();

        // Leitura mais comum: horarios livres de um vet num intervalo de datas
        builder.HasIndex(s => new { s.Estado, s.Inicio }).HasDatabaseName("IX_SLOT_ESTADO_INICIO");
    }
}
