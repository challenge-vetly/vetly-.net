using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTabelaDeIdempotencia : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// TB_IDEMPOTENCIA (§2.5). Tabela nova, nenhuma existente tocada.
        ///
        /// Guarda a resposta das rotas que nao podem executar duas vezes — reservar
        /// horario, criar cobranca, cancelar com estorno. O indice unico em
        /// (CHAVE, USUARIO_ID, ROTA) e o que impede duas requisicoes simultaneas com a
        /// mesma chave de gravarem dois registros.
        ///
        /// Retencao de 24h (§6.5). A limpeza dos vencidos entra com o VetlyBackgroundService,
        /// na onda 5; ate la os registros expirados apenas deixam de ser reaproveitados,
        /// porque a vigencia e conferida na leitura.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_IDEMPOTENCIA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CHAVE = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: false),
                    USUARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ROTA = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: false),
                    STATUS_HTTP = table.Column<byte>(type: "NUMBER(3)", nullable: false),
                    RESPOSTA = table.Column<string>(type: "CLOB", nullable: true),
                    CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EXPIRA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_IDEMPOTENCIA", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IDEMPOTENCIA_CHAVE",
                table: "TB_IDEMPOTENCIA",
                columns: new[] { "CHAVE", "USUARIO_ID", "ROTA" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IDEMPOTENCIA_EXPIRACAO",
                table: "TB_IDEMPOTENCIA",
                column: "EXPIRA_EM");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_IDEMPOTENCIA");
        }
    }
}
