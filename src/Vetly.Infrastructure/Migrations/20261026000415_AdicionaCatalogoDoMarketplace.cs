using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCatalogoDoMarketplace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ITEM_MARKETPLACE_ID",
                table: "TB_CUPOM_RESGATE",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TB_ITEM_MARKETPLACE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    NOME = table.Column<string>(type: "VARCHAR2(120)", maxLength: 120, nullable: false),
                    DESCRICAO = table.Column<string>(type: "VARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CATEGORIA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FORNECEDOR = table.Column<string>(type: "VARCHAR2(120)", maxLength: 120, nullable: false),
                    CUSTO_EM_PONTOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALOR_DE_REFERENCIA = table.Column<decimal>(type: "NUMBER(12,2)", nullable: false),
                    IMAGEM_MIDIA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    ESTOQUE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATUALIZADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_ITEM_MARKETPLACE", x => x.ID);
                });

            migrationBuilder.InsertData(
                table: "TB_ITEM_MARKETPLACE",
                columns: new[] { "ID", "ATIVO", "ATUALIZADO_EM", "CATEGORIA", "CRIADO_EM", "CUSTO_EM_PONTOS", "DESCRICAO", "ESTOQUE", "FORNECEDOR", "IMAGEM_MIDIA_ID", "NOME", "VALOR_DE_REFERENCIA" },
                values: new object[,]
                {
                    { "6f1c2a10-0001-4c3e-9a51-000000000001", true, null, 1, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 1000, "Cupom de desconto na ração seca premium para cães adultos de porte médio.", 40, "Rede de pet shops parceira", null, "Ração premium para cães adultos 10 kg", 289.90m },
                    { "6f1c2a10-0001-4c3e-9a51-000000000002", true, null, 1, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 500, "Cupom de desconto na ração seca para gatos castrados.", 60, "Rede de pet shops parceira", null, "Ração para gatos castrados 3 kg", 119.90m },
                    { "6f1c2a10-0001-4c3e-9a51-000000000003", true, null, 1, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 300, "Cupom de desconto no petisco desidratado, sem conservantes.", null, "Rede de pet shops parceira", null, "Petisco natural de frango 200 g", 34.90m },
                    { "6f1c2a10-0002-4c3e-9a51-000000000001", true, null, 2, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 800, "Cupom de desconto no antiparasitário de uso oral. Uso conforme orientação do veterinário.", 30, "Farmácia veterinária parceira", null, "Antipulgas e carrapatos (cães de 10 a 25 kg)", 159.90m },
                    { "6f1c2a10-0002-4c3e-9a51-000000000002", true, null, 2, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 300, "Cupom de desconto no vermífugo para cães e gatos. Uso conforme orientação do veterinário.", 50, "Farmácia veterinária parceira", null, "Vermífugo de amplo espectro", 49.90m },
                    { "6f1c2a10-0002-4c3e-9a51-000000000003", true, null, 2, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 1200, "Cupom de desconto no suplemento de condroitina e glucosamina.", 20, "Farmácia veterinária parceira", null, "Suplemento articular (60 comprimidos)", 189.90m },
                    { "6f1c2a10-0003-4c3e-9a51-000000000001", true, null, 3, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 600, "Cupom de desconto em um banho com tosa higiênica na rede parceira.", null, "Rede de banho e tosa parceira", null, "Banho e tosa (porte pequeno)", 80.00m },
                    { "6f1c2a10-0003-4c3e-9a51-000000000002", true, null, 3, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 300, "Cupom de desconto no shampoo para peles sensíveis.", 45, "Rede de pet shops parceira", null, "Shampoo hipoalergênico 500 ml", 59.90m },
                    { "6f1c2a10-0003-4c3e-9a51-000000000003", true, null, 3, new DateTime(2026, 10, 25, 0, 0, 0, 0, DateTimeKind.Utc), 400, "Cupom de desconto no kit de higiene bucal para cães e gatos.", 35, "Rede de pet shops parceira", null, "Kit escova e creme dental", 39.90m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ITEM_MKT_ATIVO_CATEGORIA",
                table: "TB_ITEM_MARKETPLACE",
                columns: new[] { "ATIVO", "CATEGORIA" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_ITEM_MARKETPLACE");

            migrationBuilder.DropColumn(
                name: "ITEM_MARKETPLACE_ID",
                table: "TB_CUPOM_RESGATE");
        }
    }
}
