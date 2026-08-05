using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase09_AvaliacaoNotoriedade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NOTA_MEDIA",
                table: "TB_VETERINARIO",
                type: "NUMBER(3,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TOTAL_AVALIACOES",
                table: "TB_VETERINARIO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "TB_AVALIACAO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    RESPONSAVEL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    NOTA_GERAL = table.Column<int>(type: "NUMBER(1)", nullable: false),
                    NOTA_ATENDIMENTO = table.Column<int>(type: "NUMBER(1)", nullable: true),
                    NOTA_PONTUALIDADE = table.Column<int>(type: "NUMBER(1)", nullable: true),
                    NOTA_ESTRUTURA = table.Column<int>(type: "NUMBER(1)", nullable: true),
                    NOTA_CUSTO_BENEFICIO = table.Column<int>(type: "NUMBER(1)", nullable: true),
                    COMENTARIO = table.Column<string>(type: "VARCHAR2(2000)", maxLength: 2000, nullable: true),
                    DATA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    STATUS_MODERACAO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    RESPOSTA_VETERINARIO = table.Column<string>(type: "VARCHAR2(2000)", maxLength: 2000, nullable: true),
                    DATA_RESPOSTA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    INVALIDADA = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_AVALIACAO", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AVALIACAO_CONSULTA_UNICA",
                table: "TB_AVALIACAO",
                column: "CONSULTA_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AVALIACAO_VETERINARIO",
                table: "TB_AVALIACAO",
                column: "VETERINARIO_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_AVALIACAO");

            migrationBuilder.DropColumn(
                name: "NOTA_MEDIA",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "TOTAL_AVALIACOES",
                table: "TB_VETERINARIO");
        }
    }
}
