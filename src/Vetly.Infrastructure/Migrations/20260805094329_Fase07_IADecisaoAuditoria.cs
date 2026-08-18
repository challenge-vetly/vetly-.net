using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase07_IADecisaoAuditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DIAGNOSTICO_FINAL",
                table: "TB_CONSULTA",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ESTADO_FINAL_DEFINIDO",
                table: "TB_CONSULTA",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PROTOCOLO_FINAL",
                table: "TB_CONSULTA",
                type: "CLOB",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TB_LOG_AUDITORIA_IA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CRMV = table.Column<string>(type: "VARCHAR2(15)", maxLength: 15, nullable: false),
                    TIMESTAMP = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    VERSAO_MODELO = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: false),
                    TIPO_SUGESTAO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONTEUDO_SUGERIDO = table.Column<string>(type: "CLOB", nullable: false),
                    DECISAO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    CONTEUDO_FINAL = table.Column<string>(type: "CLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LOG_AUDITORIA_IA", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LOG_AUDITORIA_IA_CONSULTA",
                table: "TB_LOG_AUDITORIA_IA",
                column: "CONSULTA_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_LOG_AUDITORIA_IA");

            migrationBuilder.DropColumn(
                name: "DIAGNOSTICO_FINAL",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "ESTADO_FINAL_DEFINIDO",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "PROTOCOLO_FINAL",
                table: "TB_CONSULTA");
        }
    }
}
