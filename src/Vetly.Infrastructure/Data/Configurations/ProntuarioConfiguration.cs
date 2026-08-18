using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Prontuario"/>.
/// Mapeia para a tabela TB_PRONTUARIO com suporte ao auto-relacionamento de versões.
/// </summary>
public class ProntuarioConfiguration : IEntityTypeConfiguration<Prontuario>
{
    public void Configure(EntityTypeBuilder<Prontuario> builder)
    {
        builder.ToTable("TB_PRONTUARIO");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(p => p.ConsultaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("CONSULTA_ID")
            .IsRequired();

        builder.Property(p => p.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        // CLOB para dados clínicos extensos (anamnese, exame físico, diagnóstico)
        builder.Property(p => p.DadosClinicos)
            .HasColumnType("CLOB")
            .HasColumnName("DADOS_CLINICOS")
            .IsRequired();

        // FK para o prontuário original — self-referencing para versionamento de correções
        builder.Property(p => p.VersaoOriginalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VERSAO_ORIGINAL_ID");

        builder.Property(p => p.DataCorrecao)
            .HasColumnName("DATA_CORRECAO");

        builder.Property(p => p.JustificativaCorrecao)
            .HasColumnType("VARCHAR2(1000)")
            .HasColumnName("JUSTIFICATIVA_CORRECAO");

        builder.Property(p => p.CrmvSolicitanteCorrecao)
            .HasColumnType("VARCHAR2(15)")
            .HasColumnName("CRMV_SOLICITANTE_CORRECAO");

        builder.Property(p => p.DataCriacao)
            .HasColumnName("DATA_CRIACAO")
            .IsRequired();

        builder.Property(p => p.AlertaSeguranca)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ALERTA_SEGURANCA")
            .IsRequired();

        builder.HasIndex(p => p.AnimalId).HasDatabaseName("IX_PRONTUARIO_ANIMAL");
        builder.HasIndex(p => p.ConsultaId).HasDatabaseName("IX_PRONTUARIO_CONSULTA");
    }
}
