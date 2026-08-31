using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// TB_AVALIACAO: avaliacao do atendimento pelo Responsavel (RN-055/RN-057).
    ///
    /// Aditiva — tabela nova, nenhuma coluna existente e tocada.
    ///
    /// IX_AVALIACAO_CONSULTA e unico e e invariante, nao otimizacao: uma avaliacao por
    /// consulta. Sem ele, duas requisicoes simultaneas passariam pela verificacao no
    /// servico e gravariam as duas.
    ///
    /// COMENTARIO_MODERADO esconde o texto sem apagar a linha, e NOTA continua
    /// contando na media — o contrario transformaria a moderacao em ferramenta para
    /// apagar critica.
    ///
    /// A reputacao em TB_VETERINARIO (NOTA_MEDIA/NUM_AVALIACOES) continua sendo campo
    /// derivado, recalculado a partir daqui a cada avaliacao. Media acumulada em campo
    /// diverge do que esta gravado assim que uma avaliacao e moderada ou corrigida.
    /// </summary>
    public partial class AdicionaAvaliacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_AVALIACAO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    EMPRESA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    NOTA = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    COMENTARIO = table.Column<string>(type: "VARCHAR2(1000)", maxLength: 1000, nullable: true),
                    COMENTARIO_MODERADO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    MOTIVO_MODERACAO = table.Column<string>(type: "VARCHAR2(300)", maxLength: 300, nullable: true),
                    RESPOSTA_VETERINARIO = table.Column<string>(type: "VARCHAR2(1000)", maxLength: 1000, nullable: true),
                    RESPONDIDA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CRIADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_AVALIACAO", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AVALIACAO_CONSULTA",
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
        }
    }
}
