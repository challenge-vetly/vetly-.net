using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade <see cref="Veterinario"/>.
/// Mapeia para a tabela TB_VETERINARIO com convenções Oracle (VARCHAR2, NUMBER, CHAR).
/// </summary>
public class VeterinarioConfiguration : IEntityTypeConfiguration<Veterinario>
{
    public void Configure(EntityTypeBuilder<Veterinario> builder)
    {
        builder.ToTable("TB_VETERINARIO");

        // PK como CHAR(36) para armazenar Guid no formato padrão "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnType("CHAR(36)")
            .HasColumnName("ID");

        builder.Property(v => v.Nome)
            .HasColumnType("VARCHAR2(200)")
            .HasColumnName("NOME")
            .IsRequired();

        builder.Property(v => v.UfAtuacao)
            .HasColumnType("CHAR(2)")
            .HasColumnName("UF_ATUACAO")
            .IsRequired();

        builder.Property(v => v.TitulacaoAcademica)
            .HasColumnType("VARCHAR2(300)")
            .HasColumnName("TITULACAO_ACADEMICA");

        // Enums persistidos como int para economia de espaço e índice eficiente no Oracle
        builder.Property(v => v.Persona)
            .HasConversion<int>()
            .HasColumnName("PERSONA")
            .IsRequired();

        builder.Property(v => v.Plano)
            .HasConversion<int>()
            .HasColumnName("PLANO")
            .IsRequired();

        // Oracle não tem tipo BOOLEAN nativo — NUMBER(1) com 0/1 é a convenção padrão
        builder.Property(v => v.Ativo)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("ATIVO")
            .IsRequired();

        builder.Property(v => v.EmpresaId)
            .HasColumnType("CHAR(36)")
            .HasColumnName("EMPRESA_ID");

        // Listas persistidas como string delimitada por ponto-e-vírgula (VARCHAR2)
        // ";" é sentinel para lista vazia — Oracle trata "" como NULL (coluna NOT NULL)
        builder.Property(v => v.Especialidades)
            .HasConversion(
                v => v.Count == 0 ? ";" : string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ComparadorDeListaDeTexto)
            .HasColumnType("VARCHAR2(1000)")
            .HasColumnName("ESPECIALIDADES");

        builder.Property(v => v.EspeciesAtendidas)
            .HasConversion(
                v => v.Count == 0 ? ";" : string.Join(';', v),
                v => v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ComparadorDeListaDeTexto)
            .HasColumnType("VARCHAR2(1000)")
            .HasColumnName("ESPECIES_ATENDIDAS");

        // Value object Crmv mapeado como owned entity — colunas ficam na mesma tabela
        builder.OwnsOne(v => v.Crmv, crmv =>
        {
            crmv.Property(c => c.Valor)
                .HasColumnName("CRMV")
                .HasColumnType("VARCHAR2(15)")
                .IsRequired();
        });

        // ── Credencial de acesso (§2.2, pendência P-05) ──────────────────────
        // Nullable: os cadastros anteriores a esta migration nao tem credencial.
        builder.Property(v => v.Email)
            .HasColumnType("VARCHAR2(254)")
            .HasColumnName("EMAIL");

        builder.Property(v => v.SenhaHash)
            .HasColumnType("VARCHAR2(255)")
            .HasColumnName("SENHA_HASH");

        builder.Property(v => v.SenhaTemporaria)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("SENHA_TEMPORARIA")
            .IsRequired();

        // Login busca por e-mail; o indice unico tambem barra duplicidade de conta
        builder.HasIndex(v => v.Email).HasDatabaseName("IX_VETERINARIO_EMAIL").IsUnique();

        // ── CRMV junto ao conselho, matching e reputação (RN-026/030/033/057/107) ──
        // Sem HasDefaultValue no modelo: a entidade sempre escreve o valor explicito, e um
        // DEFAULT de banco em enum dispara o aviso de sentinela do EF (o CLR default 0 nao e
        // membro valido). O valor das linhas ja existentes vem do defaultValue da migration.

        builder.Property(v => v.CrmvStatus)
            .HasConversion<int>()
            .HasColumnName("CRMV_STATUS")
            .IsRequired();

        builder.Property(v => v.CrmvValidadoEm)
            .HasColumnName("CRMV_VALIDADO_EM");

        builder.Property(v => v.NotaMedia)
            .HasColumnType("NUMBER(3,2)")
            .HasColumnName("NOTA_MEDIA")
            .IsRequired();

        builder.Property(v => v.NumAvaliacoes)
            .HasColumnType("NUMBER(10)")
            .HasColumnName("NUM_AVALIACOES")
            .IsRequired();

        builder.Property(v => v.MatchingStatus)
            .HasConversion<int>()
            .HasColumnName("MATCHING_STATUS")
            .IsRequired();

        builder.Property(v => v.Publicado)
            .HasColumnType("NUMBER(1)")
            .HasColumnName("PUBLICADO")
            .IsRequired();

        builder.Property(v => v.PublicadoEm)
            .HasColumnName("PUBLICADO_EM");

        // Endereço embutido na própria tabela (RN-026), mesmo padrão de owned entity
        // já usado no value object Crmv. Opcional: os cadastros anteriores à migration
        // não têm endereço, e todas as colunas ficam nullable.
        builder.OwnsOne(v => v.Endereco, endereco =>
        {
            // CEP e a propriedade que marca a existencia do dependente opcional: sem ela o
            // EF nao sabe distinguir "sem endereco" de "endereco com tudo nulo". A coluna
            // continua nullable no banco (as linhas antigas nao tem endereco).
            endereco.Property(e => e.Cep)
                .HasColumnType("VARCHAR2(9)").HasColumnName("CEP").IsRequired();
            endereco.Property(e => e.Logradouro)
                .HasColumnType("VARCHAR2(200)").HasColumnName("LOGRADOURO");
            endereco.Property(e => e.Numero)
                .HasColumnType("VARCHAR2(20)").HasColumnName("NUMERO");
            endereco.Property(e => e.Complemento)
                .HasColumnType("VARCHAR2(100)").HasColumnName("COMPLEMENTO");
            endereco.Property(e => e.Bairro)
                .HasColumnType("VARCHAR2(150)").HasColumnName("BAIRRO");
            endereco.Property(e => e.Cidade)
                .HasColumnType("VARCHAR2(150)").HasColumnName("CIDADE");
            endereco.Property(e => e.Uf)
                .HasColumnType("CHAR(2)").HasColumnName("UF");

            // NUMBER(9,6) dá ~11 cm de resolução — muito além do que o matching precisa
            endereco.Property(e => e.Latitude)
                .HasColumnType("NUMBER(9,6)").HasColumnName("LATITUDE");
            endereco.Property(e => e.Longitude)
                .HasColumnType("NUMBER(9,6)").HasColumnName("LONGITUDE");
            endereco.Property(e => e.CoordenadaRevisar)
                .HasColumnType("NUMBER(1)").HasColumnName("COORDENADA_REVISAR");

            // Índice composto para o filtro por bounding box da busca por proximidade
            // (§6.3: bounding box usa o índice, Haversine roda só sobre o conjunto reduzido)
            endereco.HasIndex(e => new { e.Latitude, e.Longitude })
                .HasDatabaseName("IX_VETERINARIO_COORDENADA");
        });

        // Índice na UF para buscas por região (GET /api/veterinarios/regiao/{uf})
        builder.HasIndex(v => v.UfAtuacao).HasDatabaseName("IX_VETERINARIO_UF");

        // Perfis elegíveis ao matching: filtro de entrada de toda busca (§6.3)
        builder.HasIndex(v => new { v.Publicado, v.MatchingStatus, v.CrmvStatus })
            .HasDatabaseName("IX_VETERINARIO_MATCHING");
    }

    // Sem ValueComparer o EF Core compara a colecao por REFERENCIA: AdicionarEspecialidade()
    // muta a mesma List<string>, o snapshot aponta para ela, e a mudanca nunca e detectada —
    // o UPDATE simplesmente nao acontece. Compara por conteudo e clona no snapshot.
    private static readonly ValueComparer<List<string>> ComparadorDeListaDeTexto = new(
        (a, b) => a != null && b != null && a.SequenceEqual(b),
        v => v.Aggregate(0, (acc, item) => HashCode.Combine(acc, item.GetHashCode())),
        v => v.ToList());
}
