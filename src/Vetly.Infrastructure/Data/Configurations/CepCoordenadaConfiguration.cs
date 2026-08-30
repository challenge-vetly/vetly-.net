using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="CepCoordenada"/> (TB_CEP_COORDENADA).
///
/// Tabela de apoio da geocodificação simulada. O seed vive na própria migration,
/// para que subir o banco do zero já deixe a busca funcional.
/// </summary>
public class CepCoordenadaConfiguration : IEntityTypeConfiguration<CepCoordenada>
{
    public void Configure(EntityTypeBuilder<CepCoordenada> builder)
    {
        builder.ToTable("TB_CEP_COORDENADA");

        builder.HasKey(c => c.Cep);
        builder.Property(c => c.Cep)
            .HasColumnType("CHAR(8)").HasColumnName("CEP").IsRequired();

        builder.Property(c => c.Latitude)
            .HasColumnType("NUMBER(9,6)").HasColumnName("LATITUDE").IsRequired();

        builder.Property(c => c.Longitude)
            .HasColumnType("NUMBER(9,6)").HasColumnName("LONGITUDE").IsRequired();

        builder.Property(c => c.Cidade)
            .HasColumnType("VARCHAR2(150)").HasColumnName("CIDADE").IsRequired();

        builder.Property(c => c.Uf)
            .HasColumnType("CHAR(2)").HasColumnName("UF").IsRequired();

        // Fallback por cidade quando o CEP exato nao esta na base
        builder.HasIndex(c => new { c.Cidade, c.Uf }).HasDatabaseName("IX_CEP_COORDENADA_CIDADE");
    }
}
