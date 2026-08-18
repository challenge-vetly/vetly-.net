using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Consulta"/>.
/// Mapeia para a tabela TB_CONSULTA com convenções Oracle.
/// </summary>
public class ConsultaConfiguration : IEntityTypeConfiguration<Consulta>
{
    public void Configure(EntityTypeBuilder<Consulta> builder)
    {
        builder.ToTable("TB_CONSULTA");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        // Indexado para consultas por período (GET /api/consultas?data=...)
        builder.Property(c => c.DataHora)
            .HasColumnName("DATA_HORA")
            .IsRequired();

        builder.Property(c => c.Modalidade)
            .HasConversion<int>()
            .HasColumnName("MODALIDADE")
            .IsRequired();

        builder.Property(c => c.TipoServico)
            .HasConversion<int>()
            .HasColumnName("TIPO_SERVICO")
            .IsRequired();

        builder.Property(c => c.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        builder.Property(c => c.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(c => c.ResponsavelId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("RESPONSAVEL_ID")
            .IsRequired();

        builder.Property(c => c.DiagnosticoValidado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("DIAGNOSTICO_VALIDADO");

        builder.Property(c => c.ProtocoloValidado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("PROTOCOLO_VALIDADO");

        builder.Property(c => c.Status)
            .HasConversion<int>()
            .HasColumnName("STATUS")
            .IsRequired();

        builder.Property(c => c.PreSintomas)
            .HasColumnType("VARCHAR2(4000)")
            .HasColumnName("PRE_SINTOMAS");

        builder.Property(c => c.LockCheckoutExpiraEm)
            .HasColumnName("LOCK_CHECKOUT_EXPIRA_EM");

        builder.Property(c => c.ContadorRemarcacoes)
            .HasColumnType("NUMBER(5)")
            .HasColumnName("CONTADOR_REMARCACOES");

        builder.Property(c => c.DataRealizada)
            .HasColumnName("DATA_REALIZADA");

        // Estado final da decisao clinica (RN-099) — v2 IA
        builder.Property(c => c.DiagnosticoFinal)
            .HasColumnType("CLOB")
            .HasColumnName("DIAGNOSTICO_FINAL");

        builder.Property(c => c.ProtocoloFinal)
            .HasColumnType("CLOB")
            .HasColumnName("PROTOCOLO_FINAL");

        builder.Property(c => c.EstadoFinalDefinido)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ESTADO_FINAL_DEFINIDO");

        // Índice composto para as buscas mais comuns: por veterinário + data
        builder.HasIndex(c => c.VeterinarioId).HasDatabaseName("IX_CONSULTA_VETERINARIO");
        builder.HasIndex(c => c.AnimalId).HasDatabaseName("IX_CONSULTA_ANIMAL");
        builder.HasIndex(c => c.DataHora).HasDatabaseName("IX_CONSULTA_DATA");
    }
}
