using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="RefreshToken"/>.
/// Mapeia para TB_REFRESH_TOKEN com convenções Oracle.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("TB_REFRESH_TOKEN");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(t => t.UsuarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("USUARIO_ID")
            .IsRequired();

        builder.Property(t => t.TipoUsuario)
            .HasConversion<int>()
            .HasColumnName("TIPO_USUARIO")
            .IsRequired();

        // SHA-256 em hexadecimal tem exatamente 64 caracteres
        builder.Property(t => t.Hash)
            .HasColumnType("VARCHAR2(64)")
            .HasColumnName("HASH")
            .IsRequired();

        builder.Property(t => t.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();
        builder.Property(t => t.ExpiraEm).HasColumnName("EXPIRA_EM").IsRequired();

        builder.Property(t => t.Revogado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("REVOGADO")
            .IsRequired();

        builder.Property(t => t.RevogadoEm).HasColumnName("REVOGADO_EM");

        builder.Property(t => t.SubstituidoPorId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("SUBSTITUIDO_POR_ID");

        // O refresh chega com o token: a busca é sempre pelo hash
        builder.HasIndex(t => t.Hash).HasDatabaseName("IX_REFRESH_TOKEN_HASH").IsUnique();

        // Revogar todos os tokens de um usuário é operação de logout e de offboarding (RN-022)
        builder.HasIndex(t => new { t.UsuarioId, t.Revogado }).HasDatabaseName("IX_REFRESH_TOKEN_USUARIO");
    }
}
