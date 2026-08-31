using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>Configuração EF Core para <see cref="Avaliacao"/> (TB_AVALIACAO).</summary>
public class AvaliacaoConfiguration : IEntityTypeConfiguration<Avaliacao>
{
    public void Configure(EntityTypeBuilder<Avaliacao> builder)
    {
        builder.ToTable("TB_AVALIACAO");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(a => a.ConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ID").IsRequired();

        builder.Property(a => a.TutorId)
            .HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID").IsRequired();

        builder.Property(a => a.VeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("VETERINARIO_ID").IsRequired();

        builder.Property(a => a.EmpresaId)
            .HasColumnType("CHAR(36)").HasColumnName("EMPRESA_ID");

        builder.Property(a => a.Nota)
            .HasColumnType("NUMBER(1)").HasColumnName("NOTA").IsRequired();

        builder.Property(a => a.Comentario)
            .HasColumnType("VARCHAR2(1000)").HasColumnName("COMENTARIO");

        builder.Property(a => a.ComentarioModerado)
            .HasColumnType("NUMBER(1)").HasColumnName("COMENTARIO_MODERADO").IsRequired();

        builder.Property(a => a.MotivoDaModeracao)
            .HasColumnType("VARCHAR2(300)").HasColumnName("MOTIVO_MODERACAO");

        builder.Property(a => a.RespostaDoVeterinario)
            .HasColumnType("VARCHAR2(1000)").HasColumnName("RESPOSTA_VETERINARIO");

        builder.Property(a => a.RespondidaEm).HasColumnName("RESPONDIDA_EM");
        // RN-059: avaliacao de consulta cancelada sai do calculo, mas a linha fica.
        // Apagar registro de reputacao abriria caminho para gestao de nota via
        // cancelamento.
        builder.Property(a => a.Valida)
            .HasColumnType("NUMBER(1)").HasColumnName("VALIDA").IsRequired();

        builder.Property(a => a.MotivoDaInvalidacao)
            .HasColumnType("VARCHAR2(200)").HasColumnName("MOTIVO_INVALIDACAO");

        builder.Property(a => a.CriadaEm).HasColumnName("CRIADA_EM").IsRequired();

        // Uma avaliação por consulta: o índice é a invariante, não otimização. Sem ele,
        // duas requisições simultâneas passariam pela verificação e gravariam as duas.
        builder.HasIndex(a => a.ConsultaId).HasDatabaseName("IX_AVALIACAO_CONSULTA").IsUnique();

        // O recálculo da reputação carrega por veterinário
        builder.HasIndex(a => a.VeterinarioId).HasDatabaseName("IX_AVALIACAO_VETERINARIO");
    }
}
