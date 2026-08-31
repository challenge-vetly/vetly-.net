using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// TB_LOG_AUDITORIA_IA e a decisao do veterinario sobre o rascunho (RN-082, §7.3).
    ///
    /// Aditiva — tabela nova e duas colunas novas em TB_RASCUNHO_IA; nenhuma coluna
    /// existente e tocada.
    ///
    /// TB_LOG_AUDITORIA_IA e append-only por contrato: a aplicacao so insere e le. Um
    /// registro que pode ser reescrito depois nao prova que houve decisao humana, e e
    /// exatamente isso que esta tabela existe para provar.
    ///
    /// CONTEUDO_FINAL guarda o prontuario inteiro como o veterinario aceitou, e nao um
    /// diff: reconstruir o que foi assinado a partir de diferencas e fragil justamente
    /// quando mais importa.
    ///
    /// IX_AUDITORIA_IA_CONSULTA NAO e unico: a mesma consulta acumula decisoes — a
    /// recusa do rascunho e, depois, o prontuario manual que a sucede.
    ///
    /// DECISAO em TB_RASCUNHO_IA nasce 1 (Pendente), e nao 0: zero nao e membro do
    /// enum, e "sugestao ainda sem decisao" e exatamente o estado das linhas
    /// existentes.
    /// </summary>
    public partial class AdicionaAuditoriaDeDecisaoIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DECIDIDO_EM",
                table: "TB_RASCUNHO_IA",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DECISAO",
                table: "TB_RASCUNHO_IA",
                type: "NUMBER(10)",
                nullable: false,
                // 1 = Pendente. Zero nao e membro do enum, e rascunho ja gravado e,
                // por definicao, rascunho que ainda nao foi decidido.
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "TB_LOG_AUDITORIA_IA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    SESSAO_CAPTURA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    RASCUNHO_IA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    DECISAO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONTEUDO_FINAL = table.Column<string>(type: "CLOB", nullable: false),
                    JUSTIFICATIVA = table.Column<string>(type: "CLOB", nullable: true),
                    ALTEROU_SUGESTAO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    MODELO = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: true),
                    REGISTRADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LOG_AUDITORIA_IA", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AUDITORIA_IA_CONSULTA",
                table: "TB_LOG_AUDITORIA_IA",
                column: "CONSULTA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AUDITORIA_IA_REGISTRADO_EM",
                table: "TB_LOG_AUDITORIA_IA",
                column: "REGISTRADO_EM");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_LOG_AUDITORIA_IA");

            migrationBuilder.DropColumn(
                name: "DECIDIDO_EM",
                table: "TB_RASCUNHO_IA");

            migrationBuilder.DropColumn(
                name: "DECISAO",
                table: "TB_RASCUNHO_IA");
        }
    }
}
