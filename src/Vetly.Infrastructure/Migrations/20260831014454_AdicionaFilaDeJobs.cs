using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaFilaDeJobs : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// TB_JOB (§11). Tabela nova, nenhuma existente tocada.
        ///
        /// E o que permite trabalho de negocio acontecer fora do ciclo da requisicao:
        /// promover a lista de espera quando um horario volta a ficar livre (RN-037) e
        /// entregar o evento do provedor de pagamento simulado (§5.1).
        ///
        /// Sobre o Oracle que ja existe, sem broker novo. Se o volume exigir, trocar por
        /// Hangfire ou Quartz nao muda os handlers — so quem os chama.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_JOB",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PAYLOAD = table.Column<string>(type: "VARCHAR2(2000)", maxLength: 2000, nullable: true),
                    EXECUTAR_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    TENTATIVAS = table.Column<byte>(type: "NUMBER(3)", nullable: false),
                    ESTADO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ULTIMO_ERRO = table.Column<string>(type: "VARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CONCLUIDO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_JOB", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JOB_FILA",
                table: "TB_JOB",
                columns: new[] { "ESTADO", "EXECUTAR_EM" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_JOB");
        }
    }
}
