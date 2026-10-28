using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Data.Configurations;

/// <summary>
/// Mapeamento do catálogo do marketplace (<c>TB_ITEM_MARKETPLACE</c>, RN-108 a RN-111)
/// e a carga inicial de nove itens, três por categoria.
///
/// A carga vai por <c>HasData</c>, que vira <c>InsertData</c> na migration: é dado de
/// catálogo versionado junto com o esquema, aplicado uma vez pelo DBA com o script,
/// e não código que roda a cada arranque da API decidindo se insere ou não.
/// </summary>
public class ItemMarketplaceConfiguration : IEntityTypeConfiguration<ItemMarketplace>
{
    /// <summary>Data fixa da carga: <c>HasData</c> exige valores constantes.</summary>
    private static readonly DateTime CargaInicial = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<ItemMarketplace> builder)
    {
        builder.ToTable("TB_ITEM_MARKETPLACE");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnType("CHAR(36)").HasColumnName("ID");

        builder.Property(i => i.Nome)
            .HasColumnType("VARCHAR2(120)").HasColumnName("NOME").IsRequired();

        builder.Property(i => i.Descricao)
            .HasColumnType("VARCHAR2(1000)").HasColumnName("DESCRICAO");

        builder.Property(i => i.Categoria)
            .HasConversion<int>().HasColumnType("NUMBER(10)").HasColumnName("CATEGORIA").IsRequired();

        builder.Property(i => i.Fornecedor)
            .HasColumnType("VARCHAR2(120)").HasColumnName("FORNECEDOR").IsRequired();

        // Pontos em NUMBER(10): int em NUMBER(1..4) o provider Oracle le como byte e
        // trunca sem erro (ver MapeamentoNumericoOracleTests)
        builder.Property(i => i.CustoEmPontos)
            .HasColumnType("NUMBER(10)").HasColumnName("CUSTO_EM_PONTOS").IsRequired();

        builder.Property(i => i.ValorDeReferencia)
            .HasColumnType("NUMBER(12,2)").HasColumnName("VALOR_DE_REFERENCIA").IsRequired();

        builder.Property(i => i.ImagemMidiaId)
            .HasColumnType("CHAR(36)").HasColumnName("IMAGEM_MIDIA_ID");

        // Token de concorrencia: dois resgates simultaneos da ultima unidade leem
        // ESTOQUE = 1 e gravariam 0 os dois — o segundo cupom sairia sem unidade. Com o
        // valor original no WHERE do UPDATE, o segundo nao encontra a linha e vira 409.
        builder.Property(i => i.Estoque)
            .HasColumnType("NUMBER(10)").HasColumnName("ESTOQUE")
            .IsConcurrencyToken();

        builder.Property(i => i.Ativo)
            .HasColumnType("NUMBER(1)").HasColumnName("ATIVO").IsRequired();

        builder.Property(i => i.CriadoEm).HasColumnName("CRIADO_EM").IsRequired();
        builder.Property(i => i.AtualizadoEm).HasColumnName("ATUALIZADO_EM");

        // O catalogo do Responsavel filtra por ativo e categoria
        builder.HasIndex(i => new { i.Ativo, i.Categoria }).HasDatabaseName("IX_ITEM_MKT_ATIVO_CATEGORIA");

        builder.HasData(CargaDoCatalogo());
    }

    /// <summary>
    /// Nove itens, três por categoria. O custo em pontos acompanha o valor do benefício
    /// (100 pontos = R$ 3,00, RN-049): o cupom é desconto sobre o item, não o item de
    /// graça — um saco de ração inteiro por pontos exigiria milhares deles.
    /// </summary>
    private static object[] CargaDoCatalogo() =>
    [
        Item("6f1c2a10-0001-4c3e-9a51-000000000001", "Ração premium para cães adultos 10 kg",
            "Cupom de desconto na ração seca premium para cães adultos de porte médio.",
            CategoriaItem.Alimentacao, "Rede de pet shops parceira", 1000, 289.90m, 40),
        Item("6f1c2a10-0001-4c3e-9a51-000000000002", "Ração para gatos castrados 3 kg",
            "Cupom de desconto na ração seca para gatos castrados.",
            CategoriaItem.Alimentacao, "Rede de pet shops parceira", 500, 119.90m, 60),
        Item("6f1c2a10-0001-4c3e-9a51-000000000003", "Petisco natural de frango 200 g",
            "Cupom de desconto no petisco desidratado, sem conservantes.",
            CategoriaItem.Alimentacao, "Rede de pet shops parceira", 300, 34.90m, null),

        Item("6f1c2a10-0002-4c3e-9a51-000000000001", "Antipulgas e carrapatos (cães de 10 a 25 kg)",
            "Cupom de desconto no antiparasitário de uso oral. Uso conforme orientação do veterinário.",
            CategoriaItem.Medicamentos, "Farmácia veterinária parceira", 800, 159.90m, 30),
        Item("6f1c2a10-0002-4c3e-9a51-000000000002", "Vermífugo de amplo espectro",
            "Cupom de desconto no vermífugo para cães e gatos. Uso conforme orientação do veterinário.",
            CategoriaItem.Medicamentos, "Farmácia veterinária parceira", 300, 49.90m, 50),
        Item("6f1c2a10-0002-4c3e-9a51-000000000003", "Suplemento articular (60 comprimidos)",
            "Cupom de desconto no suplemento de condroitina e glucosamina.",
            CategoriaItem.Medicamentos, "Farmácia veterinária parceira", 1200, 189.90m, 20),

        Item("6f1c2a10-0003-4c3e-9a51-000000000001", "Banho e tosa (porte pequeno)",
            "Cupom de desconto em um banho com tosa higiênica na rede parceira.",
            CategoriaItem.Higiene, "Rede de banho e tosa parceira", 600, 80.00m, null),
        Item("6f1c2a10-0003-4c3e-9a51-000000000002", "Shampoo hipoalergênico 500 ml",
            "Cupom de desconto no shampoo para peles sensíveis.",
            CategoriaItem.Higiene, "Rede de pet shops parceira", 300, 59.90m, 45),
        Item("6f1c2a10-0003-4c3e-9a51-000000000003", "Kit escova e creme dental",
            "Cupom de desconto no kit de higiene bucal para cães e gatos.",
            CategoriaItem.Higiene, "Rede de pet shops parceira", 400, 39.90m, 35)
    ];

    private static object Item(
        string id, string nome, string descricao, CategoriaItem categoria,
        string fornecedor, int custo, decimal valor, int? estoque) => new
    {
        Id = Guid.Parse(id),
        Nome = nome,
        Descricao = descricao,
        Categoria = categoria,
        Fornecedor = fornecedor,
        CustoEmPontos = custo,
        ValorDeReferencia = valor,
        ImagemMidiaId = (Guid?)null,
        Estoque = estoque,
        Ativo = true,
        CriadoEm = CargaInicial,
        AtualizadoEm = (DateTime?)null
    };
}
