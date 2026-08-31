using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>Configuração EF Core para <see cref="SessaoCaptura"/> (TB_SESSAO_CAPTURA).</summary>
public class SessaoCapturaConfiguration : IEntityTypeConfiguration<SessaoCaptura>
{
    public void Configure(EntityTypeBuilder<SessaoCaptura> builder)
    {
        builder.ToTable("TB_SESSAO_CAPTURA");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(s => s.ConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ID").IsRequired();

        builder.Property(s => s.Estado)
            .HasConversion<int>().HasColumnName("ESTADO").IsRequired();

        builder.Property(s => s.IniciadaEm).HasColumnName("INICIADA_EM").IsRequired();
        builder.Property(s => s.EncerradaEm).HasColumnName("ENCERRADA_EM");

        builder.Property(s => s.CapturaAtiva)
            .HasColumnType("NUMBER(1)").HasColumnName("CAPTURA_ATIVA").IsRequired();

        // Uma consulta tem no maximo uma sessao de captura: iniciar duas vezes seria
        // abrir duas janelas de gravacao sobre o mesmo atendimento (RN-008/RN-079)
        builder.HasIndex(s => s.ConsultaId).HasDatabaseName("IX_SESSAO_CAPTURA_CONSULTA").IsUnique();
    }
}

/// <summary>Configuração EF Core para <see cref="SegmentoAudio"/> (TB_SEGMENTO_AUDIO).</summary>
public class SegmentoAudioConfiguration : IEntityTypeConfiguration<SegmentoAudio>
{
    public void Configure(EntityTypeBuilder<SegmentoAudio> builder)
    {
        builder.ToTable("TB_SEGMENTO_AUDIO");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(s => s.SessaoCapturaId)
            .HasColumnType("CHAR(36)").HasColumnName("SESSAO_CAPTURA_ID").IsRequired();

        builder.Property(s => s.Sequencia)
            .HasColumnType("NUMBER(6)").HasColumnName("SEQUENCIA").IsRequired();

        builder.Property(s => s.MidiaId)
            .HasColumnType("CHAR(36)").HasColumnName("MIDIA_ID").IsRequired();

        builder.Property(s => s.DuracaoMs)
            .HasColumnType("NUMBER(10)").HasColumnName("DURACAO_MS").IsRequired();

        builder.Property(s => s.InicioRelativoMs)
            .HasColumnType("NUMBER(10)").HasColumnName("INICIO_RELATIVO_MS").IsRequired();

        builder.Property(s => s.Estado)
            .HasConversion<int>().HasColumnName("ESTADO").IsRequired();

        builder.Property(s => s.FalhaMotivo)
            .HasConversion<int?>().HasColumnName("FALHA_MOTIVO");

        builder.Property(s => s.Tentativas)
            .HasColumnType("NUMBER(3)").HasColumnName("TENTATIVAS").IsRequired();

        builder.Property(s => s.CallbackTokenHash)
            .HasColumnType("VARCHAR2(64)").HasColumnName("CALLBACK_TOKEN_HASH");

        builder.Property(s => s.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();

        // O mesmo numero de sequencia nao se repete numa sessao: reenvio de segmento
        // duplicaria o texto na transcricao final
        builder.HasIndex(s => new { s.SessaoCapturaId, s.Sequencia })
            .HasDatabaseName("IX_SEGMENTO_SESSAO_SEQUENCIA").IsUnique();

        builder.HasIndex(s => s.Estado).HasDatabaseName("IX_SEGMENTO_ESTADO");
    }
}

/// <summary>Configuração EF Core para <see cref="Transcricao"/> (TB_TRANSCRICAO).</summary>
public class TranscricaoConfiguration : IEntityTypeConfiguration<Transcricao>
{
    public void Configure(EntityTypeBuilder<Transcricao> builder)
    {
        builder.ToTable("TB_TRANSCRICAO");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(t => t.SegmentoAudioId)
            .HasColumnType("CHAR(36)").HasColumnName("SEGMENTO_AUDIO_ID").IsRequired();

        // CLOB: fala de consulta nao cabe em VARCHAR2
        builder.Property(t => t.Texto)
            .HasColumnType("CLOB").HasColumnName("TEXTO").IsRequired();

        builder.Property(t => t.Confianca)
            .HasColumnType("NUMBER(4,3)").HasColumnName("CONFIANCA");

        builder.Property(t => t.Trechos)
            .HasColumnType("CLOB").HasColumnName("TRECHOS");

        builder.Property(t => t.Motor)
            .HasColumnType("VARCHAR2(100)").HasColumnName("MOTOR");

        builder.Property(t => t.CriadaEm).HasColumnName("CRIADA_EM").IsRequired();

        // Um segmento tem uma transcricao: callback reentregue nao pode duplicar texto
        builder.HasIndex(t => t.SegmentoAudioId).HasDatabaseName("IX_TRANSCRICAO_SEGMENTO").IsUnique();
    }
}
