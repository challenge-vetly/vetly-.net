using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="RegistroIdempotencia"/> (TB_IDEMPOTENCIA).
/// </summary>
public class RegistroIdempotenciaConfiguration : IEntityTypeConfiguration<RegistroIdempotencia>
{
    public void Configure(EntityTypeBuilder<RegistroIdempotencia> builder)
    {
        builder.ToTable("TB_IDEMPOTENCIA");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(r => r.Chave)
            .HasColumnType("VARCHAR2(100)").HasColumnName("CHAVE").IsRequired();

        builder.Property(r => r.UsuarioId)
            .HasColumnType("CHAR(36)").HasColumnName("USUARIO_ID").IsRequired();

        builder.Property(r => r.Rota)
            .HasColumnType("VARCHAR2(200)").HasColumnName("ROTA").IsRequired();

        builder.Property(r => r.StatusHttp)
            .HasColumnType("NUMBER(3)").HasColumnName("STATUS_HTTP").IsRequired();

        // CLOB: o corpo da resposta nao cabe em VARCHAR2(4000)
        builder.Property(r => r.Resposta)
            .HasColumnType("CLOB").HasColumnName("RESPOSTA");

        builder.Property(r => r.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();
        builder.Property(r => r.ExpiraEm).HasColumnName("EXPIRA_EM").IsRequired();

        // O trio identifica a requisicao: a mesma chave de outra pessoa, ou da mesma
        // pessoa em outra rota, e outra requisicao. O indice unico e o que garante que
        // duas chamadas simultaneas nao gravem dois registros.
        builder.HasIndex(r => new { r.Chave, r.UsuarioId, r.Rota })
            .HasDatabaseName("IX_IDEMPOTENCIA_CHAVE").IsUnique();

        // Limpeza dos registros vencidos (retencao de 24h, §6.5)
        builder.HasIndex(r => r.ExpiraEm).HasDatabaseName("IX_IDEMPOTENCIA_EXPIRACAO");
    }
}
