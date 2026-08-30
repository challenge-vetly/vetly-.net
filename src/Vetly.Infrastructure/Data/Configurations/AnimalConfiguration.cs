using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;
using Vetly.Domain.ValueObjects;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Animal"/>.
/// Mapeia para a tabela TB_ANIMAL com convenções Oracle.
/// </summary>
public class AnimalConfiguration : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("TB_ANIMAL");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(a => a.Nome)
            .HasColumnType("VARCHAR2(200)")
            .HasColumnName("NOME")
            .IsRequired();

        builder.Property(a => a.Especie)
            .HasColumnType("VARCHAR2(100)")
            .HasColumnName("ESPECIE")
            .IsRequired();

        builder.Property(a => a.Raca)
            .HasColumnType("VARCHAR2(100)")
            .HasColumnName("RACA")
            .IsRequired();

        builder.Property(a => a.DataNascimento)
            .HasColumnName("DATA_NASCIMENTO")
            .IsRequired();

        builder.Property(a => a.TutorId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("TUTOR_ID")
            .IsRequired();

        // NUMBER(1) para boolean — padrão Oracle
        builder.Property(a => a.Ativo)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ATIVO")
            .IsRequired();

        // Alertas armazenados como string delimitada; ";" é sentinel para lista vazia
        // (Oracle trata "" como NULL; Split com RemoveEmptyEntries lê ";" de volta como [])
        builder.Property(a => a.AlertasAtivos)
            .HasConversion(
                v => v.Count == 0 ? ";" : string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList())
            .HasColumnType("VARCHAR2(2000)")
            .HasColumnName("ALERTAS_ATIVOS");

        // ── Perfil clínico (RN-081, RN-046) ──────────────────────────────────

        // NUMBER(5,2) cobre de 0,01 kg a 999,99 kg — folga suficiente para qualquer espécie.
        // Nullable: as linhas anteriores à migration não têm peso; a API exige na criação.
        builder.Property(a => a.PesoKg)
            .HasColumnType("NUMBER(5,2)")
            .HasColumnName("PESO_KG");

        // NUMBER(10) para enum — mesmo tipo ja usado em MODALIDADE (TB_CONSULTA)
        builder.Property(a => a.Sexo)
            .HasColumnType("NUMBER(10)")
            .HasColumnName("SEXO");

        builder.Property(a => a.Castrado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("CASTRADO");

        builder.Property(a => a.FotoMidiaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("FOTO_MIDIA_ID");

        // Mesmo sentinel ";" usado em ALERTAS_ATIVOS: Oracle trata "" como NULL
        builder.Property(a => a.Alergias)
            .HasConversion(
                v => v.Count == 0 ? ";" : string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ComparadorDeListaDeTexto)
            .HasColumnType("VARCHAR2(2000)")
            .HasColumnName("ALERGIAS");

        builder.Property(a => a.CondicoesPreexistentes)
            .HasConversion(
                v => v.Count == 0 ? ";" : string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ComparadorDeListaDeTexto)
            .HasColumnType("VARCHAR2(2000)")
            .HasColumnName("CONDICOES_PREEXISTENTES");

        // Carteira de vacinação é lista de objetos: serializada como JSON em CLOB.
        // "[]" explícito porque Oracle leria string vazia como NULL.
        builder.Property(a => a.CarteiraVacinacao)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<RegistroVacinacao>>(v, JsonOptions) ?? new List<RegistroVacinacao>(),
                ComparadorDeCarteira)
            .HasColumnType("CLOB")
            .HasColumnName("CARTEIRA_VACINACAO");

        // Índice para buscar todos os animais de um tutor eficientemente
        builder.HasIndex(a => a.TutorId).HasDatabaseName("IX_ANIMAL_TUTOR");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    // Sem ValueComparer o EF Core nao detecta mutacao dentro da colecao (Add/Remove no
    // mesmo objeto) e a alteracao nao chega ao banco. Compara por conteudo e clona no snapshot.
    private static readonly ValueComparer<List<string>> ComparadorDeListaDeTexto = new(
        (a, b) => a != null && b != null && a.SequenceEqual(b),
        v => v.Aggregate(0, (acc, item) => HashCode.Combine(acc, item.GetHashCode())),
        v => v.ToList());

    private static readonly ValueComparer<List<RegistroVacinacao>> ComparadorDeCarteira = new(
        (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
        v => JsonSerializer.Serialize(v, JsonOptions).GetHashCode(),
        v => JsonSerializer.Deserialize<List<RegistroVacinacao>>(
                 JsonSerializer.Serialize(v, JsonOptions), JsonOptions) ?? new List<RegistroVacinacao>());
}
