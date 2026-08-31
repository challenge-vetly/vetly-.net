using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTabelaDeMidia : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// TB_MIDIA (§2.6). Tabela nova, nenhuma existente tocada.
        ///
        /// Guarda apenas o METADADO do arquivo — chave no storage, tipo, tamanho e
        /// retencao. Os bytes ficam no storage de objetos e nunca passam pelo processo
        /// da API: CLOB no Oracle nao e lugar para binario de audio.
        ///
        /// RETENCAO_ATE existe por causa do audio da consulta, que sai em 30 dias (P-06);
        /// conteudo clinico nao expira, por guarda regulatoria (RN-062).
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_MIDIA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CHAVE_STORAGE = table.Column<string>(type: "VARCHAR2(300)", maxLength: 300, nullable: false),
                    CONTENT_TYPE = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    TAMANHO_BYTES = table.Column<long>(type: "NUMBER(19)", nullable: true),
                    CRIADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    RETENCAO_ATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_MIDIA", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MIDIA_CHAVE",
                table: "TB_MIDIA",
                column: "CHAVE_STORAGE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MIDIA_CONSULTA",
                table: "TB_MIDIA",
                column: "CONSULTA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MIDIA_RETENCAO",
                table: "TB_MIDIA",
                columns: new[] { "STATUS", "RETENCAO_ATE" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_MIDIA");
        }
    }
}
