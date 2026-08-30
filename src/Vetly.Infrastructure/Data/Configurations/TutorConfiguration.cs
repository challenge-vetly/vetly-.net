using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Tutor"/>.
/// Mapeia para a tabela TB_TUTOR com convenções Oracle.
/// </summary>
public class TutorConfiguration : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> builder)
    {
        builder.ToTable("TB_TUTOR");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(t => t.Nome)
            .HasColumnType("VARCHAR2(200)")
            .HasColumnName("NOME")
            .IsRequired();

        // VARCHAR2(254) segue o limite máximo de e-mail definido na RFC 5321
        builder.Property(t => t.Email)
            .HasColumnType("VARCHAR2(254)")
            .HasColumnName("EMAIL")
            .IsRequired();

        builder.Property(t => t.Telefone)
            .HasColumnType("VARCHAR2(20)")
            .HasColumnName("TELEFONE")
            .IsRequired();

        // Campos de consentimento LGPD — NUMBER(1) para boolean Oracle
        builder.Property(t => t.ConsentimentoAtendimento)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CONSENTIMENTO_ATENDIMENTO");

        builder.Property(t => t.ConsentimentoLembretes)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CONSENTIMENTO_LEMBRETES");

        builder.Property(t => t.ConsentimentoCompartilhamento)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CONSENTIMENTO_COMPARTILHAMENTO");

        builder.Property(t => t.ConsentimentoPromocoes)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CONSENTIMENTO_PROMOCOES");

        builder.Property(t => t.ConsentimentoDadosAgregados)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CONSENTIMENTO_DADOS_AGREGADOS");

        builder.Property(t => t.DataConsentimento)
            .HasColumnName("DATA_CONSENTIMENTO");

        // Datas por finalidade: a LGPD exige registro de concessao E de revogacao
        // por finalidade (RN-061/RN-062), o que um unico DATA_CONSENTIMENTO nao cobre.
        builder.Property(t => t.DataConcessaoAtendimento).HasColumnName("DATA_CONCESSAO_ATENDIMENTO");
        builder.Property(t => t.DataRevogacaoAtendimento).HasColumnName("DATA_REVOGACAO_ATENDIMENTO");
        builder.Property(t => t.DataConcessaoLembretes).HasColumnName("DATA_CONCESSAO_LEMBRETES");
        builder.Property(t => t.DataRevogacaoLembretes).HasColumnName("DATA_REVOGACAO_LEMBRETES");
        builder.Property(t => t.DataConcessaoCompartilhamento).HasColumnName("DATA_CONCESSAO_COMPARTILHAMENTO");
        builder.Property(t => t.DataRevogacaoCompartilhamento).HasColumnName("DATA_REVOGACAO_COMPARTILHAMENTO");
        builder.Property(t => t.DataConcessaoPromocoes).HasColumnName("DATA_CONCESSAO_PROMOCOES");
        builder.Property(t => t.DataRevogacaoPromocoes).HasColumnName("DATA_REVOGACAO_PROMOCOES");
        builder.Property(t => t.DataConcessaoDadosAgregados).HasColumnName("DATA_CONCESSAO_DADOS_AGREGADOS");
        builder.Property(t => t.DataRevogacaoDadosAgregados).HasColumnName("DATA_REVOGACAO_DADOS_AGREGADOS");

        // Hash da senha do app. Nullable: cadastros de balcao ainda nao tem credencial.
        builder.Property(t => t.SenhaHash)
            .HasColumnType("VARCHAR2(255)")
            .HasColumnName("SENHA_HASH");

        builder.Property(t => t.Ativo)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ATIVO");

        // Índice no e-mail para buscas por e-mail e validação de unicidade
        builder.HasIndex(t => t.Email).HasDatabaseName("IX_TUTOR_EMAIL").IsUnique();
    }
}
