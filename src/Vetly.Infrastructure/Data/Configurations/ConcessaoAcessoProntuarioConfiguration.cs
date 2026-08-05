using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="ConcessaoAcessoProntuario"/>.
/// Mapeia para a tabela TB_CONCESSAO_ACESSO_PRONTUARIO com convenções Oracle.
/// </summary>
public class ConcessaoAcessoProntuarioConfiguration : IEntityTypeConfiguration<ConcessaoAcessoProntuario>
{
    public void Configure(EntityTypeBuilder<ConcessaoAcessoProntuario> builder)
    {
        builder.ToTable("TB_CONCESSAO_ACESSO_PRONTUARIO");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(c => c.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(c => c.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        builder.Property(c => c.ConsultaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("CONSULTA_ID")
            .IsRequired();

        builder.Property(c => c.BaseAcesso)
            .HasConversion<int>()
            .HasColumnName("BASE_ACESSO")
            .IsRequired();

        builder.Property(c => c.ConcedidoEm)
            .HasColumnName("CONCEDIDO_EM")
            .IsRequired();

        builder.Property(c => c.ExpiraEm)
            .HasColumnName("EXPIRA_EM")
            .IsRequired();

        builder.Property(c => c.Revogada)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("REVOGADA");

        // Acelera a busca da concessao ativa por par vet+animal (ObterAtivaAsync)
        builder.HasIndex(c => new { c.VeterinarioId, c.AnimalId })
            .HasDatabaseName("IX_CONCESSAO_VETERINARIO_ANIMAL");
    }
}
