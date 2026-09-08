using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para <see cref="AgendaConfig"/> (TB_AGENDA_CONFIG).
/// </summary>
public class AgendaConfigConfiguration : IEntityTypeConfiguration<AgendaConfig>
{
    public void Configure(EntityTypeBuilder<AgendaConfig> builder)
    {
        builder.ToTable("TB_AGENDA_CONFIG");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(a => a.VeterinarioId)
            .HasColumnType("CHAR(36)").HasColumnName("VETERINARIO_ID").IsRequired();

        // Flags de dias da semana num unico NUMBER, em vez de sete colunas
        builder.Property(a => a.Dias)
            .HasConversion<int>().HasColumnName("DIAS").IsRequired();

        // Horario em minutos desde a meia-noite: inteiro simples, sem depender de
        // suporte a TimeOnly no provider Oracle.
        //
        // SEM HasColumnType, e isso e deliberado. Declarar "NUMBER(4)" fazia o provider
        // Oracle escolher um mapeamento de UM BYTE pela precisao — o modelo do EF
        // passava a tratar a propriedade como byte, ainda que a entidade seja int, e
        // todo valor acima de 255 era truncado modulo 256 NA ESCRITA, em silencio.
        // 08:00 (480 min) virava 224 no banco, ou seja 03:44; 18:00 (1080) virava 56.
        // A coluna NUMBER(4) comportava o valor — quem truncava era o mapeamento.
        //
        // Sem o tipo explicito, o EF usa o mapeamento natural de int e CLR e coluna
        // param de discordar. A suite nao pegava isso porque os testes de integracao
        // rodam sobre InMemory, que nao aplica mapeamento de tipo do Oracle.
        builder.Property(a => a.InicioEmMinutos)
            .HasColumnName("INICIO_EM_MINUTOS").IsRequired();

        builder.Property(a => a.FimEmMinutos)
            .HasColumnName("FIM_EM_MINUTOS").IsRequired();

        builder.Property(a => a.DuracaoMinutos)
            .HasColumnName("DURACAO_MINUTOS").IsRequired();

        builder.Property(a => a.IntervaloMinutos)
            .HasColumnName("INTERVALO_MINUTOS").IsRequired();

        builder.Property(a => a.AtualizadaEm).HasColumnName("ATUALIZADA_EM").IsRequired();

        // Um veterinario tem uma configuracao de agenda
        builder.HasIndex(a => a.VeterinarioId).HasDatabaseName("IX_AGENDA_CONFIG_VETERINARIO").IsUnique();
    }
}
