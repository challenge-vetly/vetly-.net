using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase10_Fidelidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_OBRIGACAO_PET",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA_LIMITE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    DATA_CUMPRIMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_OBRIGACAO_PET", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_PONTOS_FIDELIDADE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    RESPONSAVEL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ORIGEM = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PONTOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EXPIRA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ESTORNADO = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_PONTOS_FIDELIDADE", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OBRIGACAO_PET_ANIMAL",
                table: "TB_OBRIGACAO_PET",
                column: "ANIMAL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PONTOS_FIDELIDADE_CONSULTA",
                table: "TB_PONTOS_FIDELIDADE",
                column: "CONSULTA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PONTOS_FIDELIDADE_RESPONSAVEL",
                table: "TB_PONTOS_FIDELIDADE",
                column: "RESPONSAVEL_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_OBRIGACAO_PET");

            migrationBuilder.DropTable(
                name: "TB_PONTOS_FIDELIDADE");
        }
    }
}
