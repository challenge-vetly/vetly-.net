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

        builder.Property(c => c.VeterinarioId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("VETERINARIO_ID")
            .IsRequired();

        builder.Property(c => c.AnimalId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ANIMAL_ID")
            .IsRequired();

        builder.Property(c => c.TutorId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("TUTOR_ID")
            .IsRequired();

        builder.Property(c => c.DiagnosticoValidado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("DIAGNOSTICO_VALIDADO");

        builder.Property(c => c.ProtocoloValidado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("PROTOCOLO_VALIDADO");

        builder.Property(c => c.StatusPagamento)
            .HasConversion<int>()
            .HasColumnName("STATUS_PAGAMENTO")
            .IsRequired();

        // ── Checkout (RN-003/RN-032/RN-035/RN-040) ───────────────────────────
        builder.Property(c => c.SlotId)
            .HasColumnType("CHAR(36)").HasColumnName("SLOT_ID");

        builder.Property(c => c.ServicoId)
            .HasColumnType("CHAR(36)").HasColumnName("SERVICO_ID");

        builder.Property(c => c.EmpresaId)
            .HasColumnType("CHAR(36)").HasColumnName("EMPRESA_ID");

        // ── Remarcacao e pre-sintomas (RN-036/RN-043) ────────────────────────

        builder.Property(c => c.ContadorRemarcacoes)
            .HasColumnType("NUMBER(2)").HasColumnName("CONTADOR_REMARCACOES").IsRequired();

        // CLOB: o texto guiado pode ser longo, e a mídia vem por lista de ids
        builder.Property(c => c.PreSintomas)
            .HasColumnType("CLOB").HasColumnName("PRE_SINTOMAS");

        builder.Property(c => c.PreSintomasMidias)
            .HasColumnType("VARCHAR2(2000)").HasColumnName("PRE_SINTOMAS_MIDIAS");

        builder.Property(c => c.IniciadaEm).HasColumnName("INICIADA_EM");
        builder.Property(c => c.EncerradaEm).HasColumnName("ENCERRADA_EM");

        builder.Property(c => c.Origem)
            .HasConversion<int>().HasColumnName("ORIGEM").IsRequired();

        // Nulo em tudo que nao e retorno, e por isso sem indice: a leitura por origem
        // e rara, e um indice quase todo nulo custaria escrita sem pagar leitura.
        builder.Property(c => c.ConsultaOrigemId)
            .HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ORIGEM_ID");

        // Estado da consulta na maquina de estados (RN-035/RN-038).
        // Fonte de verdade; CANCELADA e FINALIZADA seguem por dupla escrita.
        builder.Property(c => c.Status)
            .HasConversion<int>()
            .HasColumnName("STATUS")
            .IsRequired();

        // Consultas por estado sao a leitura mais comum da agenda e do dashboard
        builder.HasIndex(c => c.Status).HasDatabaseName("IX_CONSULTA_STATUS");

        builder.Property(c => c.Cancelada)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CANCELADA");

        builder.Property(c => c.Finalizada)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("FINALIZADA");

        // Índice composto para as buscas mais comuns: por veterinário + data
        builder.HasIndex(c => c.VeterinarioId).HasDatabaseName("IX_CONSULTA_VETERINARIO");
        builder.HasIndex(c => c.AnimalId).HasDatabaseName("IX_CONSULTA_ANIMAL");
        builder.HasIndex(c => c.DataHora).HasDatabaseName("IX_CONSULTA_DATA");
    }
}
