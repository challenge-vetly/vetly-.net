using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        builder.Property(s => s.DespachadoEm).HasColumnName("DESPACHADO_EM");

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

/// <summary>Configuração EF Core para <see cref="RascunhoIa"/> (TB_RASCUNHO_IA).</summary>
public class RascunhoIaConfiguration : IEntityTypeConfiguration<RascunhoIa>
{
    public void Configure(EntityTypeBuilder<RascunhoIa> builder)
    {
        builder.ToTable("TB_RASCUNHO_IA");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(r => r.SessaoCapturaId)
            .HasColumnType("CHAR(36)").HasColumnName("SESSAO_CAPTURA_ID").IsRequired();

        builder.Property(r => r.ConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ID").IsRequired();

        // CLOB em todo campo clínico: prontuário de consulta não cabe em VARCHAR2
        builder.Property(r => r.Anamnese)
            .HasColumnType("CLOB").HasColumnName("ANAMNESE").IsRequired();

        builder.Property(r => r.ExameFisico)
            .HasColumnType("CLOB").HasColumnName("EXAME_FISICO").IsRequired();

        builder.Property(r => r.Conduta)
            .HasColumnType("CLOB").HasColumnName("CONDUTA").IsRequired();

        builder.Property(r => r.Orientacoes)
            .HasColumnType("CLOB").HasColumnName("ORIENTACOES").IsRequired();

        // O texto de origem fica junto do rascunho: é o que permite auditar depois
        builder.Property(r => r.TextoOrigem)
            .HasColumnType("CLOB").HasColumnName("TEXTO_ORIGEM").IsRequired();

        // ";" explícito porque Oracle leria string vazia como NULL
        builder.Property(r => r.HipotesesDiagnosticas)
            .HasConversion(
                v => v.Count == 0 ? ";" : string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ComparadorDeListaDeTexto)
            .HasColumnType("VARCHAR2(2000)").HasColumnName("HIPOTESES_DIAGNOSTICAS").IsRequired();

        builder.Property(r => r.Avisos)
            .HasConversion(
                v => v.Count == 0 ? ";" : string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ComparadorDeListaDeTexto)
            .HasColumnType("VARCHAR2(500)").HasColumnName("AVISOS").IsRequired();

        builder.Property(r => r.Modelo)
            .HasColumnType("VARCHAR2(100)").HasColumnName("MODELO");

        builder.Property(r => r.Parcial)
            .HasColumnType("NUMBER(1)").HasColumnName("PARCIAL").IsRequired();

        builder.Property(r => r.Decisao)
            .HasConversion<int>().HasColumnName("DECISAO").IsRequired();

        builder.Property(r => r.DecididoEm).HasColumnName("DECIDIDO_EM");

        builder.Property(r => r.GeradoEm).HasColumnName("GERADO_EM").IsRequired();

        builder.Property(r => r.DuracaoMs)
            .HasColumnType("NUMBER(10)").HasColumnName("DURACAO_MS").IsRequired();

        // Uma sessão gera um rascunho: job reentregue não pode produzir um segundo
        builder.HasIndex(r => r.SessaoCapturaId).HasDatabaseName("IX_RASCUNHO_SESSAO").IsUnique();

        builder.HasIndex(r => r.ConsultaId).HasDatabaseName("IX_RASCUNHO_CONSULTA").IsUnique();
    }

    /// <summary>
    /// Sem comparador o EF compara a lista por referência e a mutação in-place nunca
    /// é persistida.
    /// </summary>
    private static readonly ValueComparer<List<string>> ComparadorDeListaDeTexto = new(
        (a, b) => a != null && b != null && a.SequenceEqual(b),
        v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
        v => v.ToList());
}

/// <summary>
/// Configuração EF Core para <see cref="LogAuditoriaIa"/> (TB_LOG_AUDITORIA_IA).
///
/// Tabela append-only: nada aqui é atualizado nem removido pela aplicação.
/// </summary>
public class LogAuditoriaIaConfiguration : IEntityTypeConfiguration<LogAuditoriaIa>
{
    public void Configure(EntityTypeBuilder<LogAuditoriaIa> builder)
    {
        builder.ToTable("TB_LOG_AUDITORIA_IA");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(l => l.ConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ID").IsRequired();

        builder.Property(l => l.SessaoCapturaId)
            .HasColumnType("CHAR(36)").HasColumnName("SESSAO_CAPTURA_ID");

        builder.Property(l => l.RascunhoIaId)
            .HasColumnType("CHAR(36)").HasColumnName("RASCUNHO_IA_ID");

        builder.Property(l => l.VeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("VETERINARIO_ID");

        builder.Property(l => l.Decisao)
            .HasConversion<int>().HasColumnName("DECISAO").IsRequired();

        // O conteúdo final inteiro, e não um diff: reconstruir o que foi aceito a
        // partir de diferenças é frágil justamente quando mais importa
        builder.Property(l => l.ConteudoFinal)
            .HasColumnType("CLOB").HasColumnName("CONTEUDO_FINAL").IsRequired();

        builder.Property(l => l.Justificativa)
            .HasColumnType("CLOB").HasColumnName("JUSTIFICATIVA");

        builder.Property(l => l.AlterouSugestao)
            .HasColumnType("NUMBER(1)").HasColumnName("ALTEROU_SUGESTAO").IsRequired();

        builder.Property(l => l.Modelo)
            .HasColumnType("VARCHAR2(100)").HasColumnName("MODELO");

        builder.Property(l => l.RegistradoEm).HasColumnName("REGISTRADO_EM").IsRequired();

        // Não é único: a mesma consulta pode acumular decisões — recusa do rascunho e,
        // depois, o prontuário manual que a sucede
        builder.HasIndex(l => l.ConsultaId).HasDatabaseName("IX_AUDITORIA_IA_CONSULTA");

        builder.HasIndex(l => l.RegistradoEm).HasDatabaseName("IX_AUDITORIA_IA_REGISTRADO_EM");
    }
}
