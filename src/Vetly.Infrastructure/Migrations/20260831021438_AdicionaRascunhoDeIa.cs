using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// TB_RASCUNHO_IA: prontuario estruturado pela IA a partir da transcricao da
    /// consulta (RN-080, §7.3).
    ///
    /// Aditiva — tabela nova, nenhuma coluna existente e tocada.
    ///
    /// TEXTO_ORIGEM guarda a transcricao que alimentou a estruturacao. Fica junto do
    /// rascunho de proposito: sem ela nao ha como conferir depois se a IA produziu
    /// algo que nao foi dito na consulta, e sugestao que chega ao prontuario precisa
    /// ser auditavel (RN-082).
    ///
    /// HIPOTESES_DIAGNOSTICAS e AVISOS sao listas serializadas com ";" quando vazias:
    /// no Oracle a string vazia E NULL, e a coluna e NOT NULL.
    ///
    /// Os dois indices unicos sao invariantes, nao otimizacao: uma sessao gera um
    /// rascunho, e job reentregue nao pode produzir um segundo sobre o mesmo
    /// atendimento.
    /// </summary>
    public partial class AdicionaRascunhoDeIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_RASCUNHO_IA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    SESSAO_CAPTURA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANAMNESE = table.Column<string>(type: "CLOB", nullable: false),
                    EXAME_FISICO = table.Column<string>(type: "CLOB", nullable: false),
                    HIPOTESES_DIAGNOSTICAS = table.Column<string>(type: "VARCHAR2(2000)", nullable: false),
                    CONDUTA = table.Column<string>(type: "CLOB", nullable: false),
                    ORIENTACOES = table.Column<string>(type: "CLOB", nullable: false),
                    TEXTO_ORIGEM = table.Column<string>(type: "CLOB", nullable: false),
                    MODELO = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: true),
                    PARCIAL = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    AVISOS = table.Column<string>(type: "VARCHAR2(500)", nullable: false),
                    GERADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DURACAO_MS = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_RASCUNHO_IA", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RASCUNHO_CONSULTA",
                table: "TB_RASCUNHO_IA",
                column: "CONSULTA_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RASCUNHO_SESSAO",
                table: "TB_RASCUNHO_IA",
                column: "SESSAO_CAPTURA_ID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_RASCUNHO_IA");
        }
    }
}
