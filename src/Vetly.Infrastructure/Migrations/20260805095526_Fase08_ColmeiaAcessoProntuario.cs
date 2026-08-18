using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase08_ColmeiaAcessoProntuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_CONCESSAO_ACESSO_PRONTUARIO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    BASE_ACESSO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONCEDIDO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EXPIRA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    REVOGADA = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_CONCESSAO_ACESSO_PRONTUARIO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_LOG_ACESSO_PRONTUARIO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    DATA_HORA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CONTEXTO = table.Column<string>(type: "VARCHAR2(500)", maxLength: 500, nullable: false),
                    BASE_ACESSO = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LOG_ACESSO_PRONTUARIO", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONCESSAO_VETERINARIO_ANIMAL",
                table: "TB_CONCESSAO_ACESSO_PRONTUARIO",
                columns: new[] { "VETERINARIO_ID", "ANIMAL_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_LOG_ACESSO_PRONTUARIO_ANIMAL",
                table: "TB_LOG_ACESSO_PRONTUARIO",
                column: "ANIMAL_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_CONCESSAO_ACESSO_PRONTUARIO");

            migrationBuilder.DropTable(
                name: "TB_LOG_ACESSO_PRONTUARIO");
        }
    }
}
