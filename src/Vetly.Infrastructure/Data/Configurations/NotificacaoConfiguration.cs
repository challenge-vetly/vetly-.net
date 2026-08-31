using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>Configuração EF Core para <see cref="Notificacao"/> (TB_NOTIFICACAO).</summary>
public class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("TB_NOTIFICACAO");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(n => n.TutorId)
            .HasColumnType("CHAR(36)").HasColumnName("TUTOR_ID").IsRequired();

        builder.Property(n => n.Tipo)
            .HasConversion<int>().HasColumnName("TIPO").IsRequired();

        builder.Property(n => n.Titulo)
            .HasColumnType("VARCHAR2(120)").HasColumnName("TITULO").IsRequired();

        builder.Property(n => n.Corpo)
            .HasColumnType("VARCHAR2(500)").HasColumnName("CORPO").IsRequired();

        builder.Property(n => n.Status)
            .HasConversion<int>().HasColumnName("STATUS").IsRequired();

        builder.Property(n => n.AnimalId)
            .HasColumnType("CHAR(36)").HasColumnName("ANIMAL_ID");

        builder.Property(n => n.ConsultaId)
            .HasColumnType("CHAR(36)").HasColumnName("CONSULTA_ID");

        builder.Property(n => n.Destino)
            .HasColumnType("VARCHAR2(200)").HasColumnName("DESTINO");

        builder.Property(n => n.AgendadaPara).HasColumnName("AGENDADA_PARA").IsRequired();
        builder.Property(n => n.EnviadaEm).HasColumnName("ENVIADA_EM");
        builder.Property(n => n.LidaEm).HasColumnName("LIDA_EM");

        builder.Property(n => n.Tentativas)
            .HasColumnType("NUMBER(3)").HasColumnName("TENTATIVAS").IsRequired();

        builder.Property(n => n.UltimoErro)
            .HasColumnType("VARCHAR2(300)").HasColumnName("ULTIMO_ERRO");

        builder.Property(n => n.CriadaEm).HasColumnName("CRIADA_EM").IsRequired();

        // A caixa de entrada carrega por Responsável; a rotina de envio varre por
        // status e hora agendada
        builder.HasIndex(n => new { n.TutorId, n.CriadaEm })
            .HasDatabaseName("IX_NOTIFICACAO_TUTOR_CRIADA");

        builder.HasIndex(n => new { n.Status, n.AgendadaPara })
            .HasDatabaseName("IX_NOTIFICACAO_STATUS_AGENDADA");

        // A regua de lembretes pergunta "ja avisei sobre este animal?"
        builder.HasIndex(n => new { n.AnimalId, n.Tipo })
            .HasDatabaseName("IX_NOTIFICACAO_ANIMAL_TIPO");
    }
}
