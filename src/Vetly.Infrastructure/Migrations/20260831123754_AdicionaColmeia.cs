using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// TB_ACESSO_COLMEIA e TB_LOG_ACESSO_COLMEIA: o historico do animal atravessando
    /// clinicas, sob autorizacao do Responsavel (RN-090/RN-105).
    ///
    /// Aditiva — duas tabelas novas, nenhuma coluna existente e tocada.
    ///
    /// EXPIRA_EM e NOT NULL de proposito: toda concessao nasce com prazo. Acesso
    /// clinico que nao expira sozinho e acesso que ninguem lembra de revogar.
    ///
    /// TB_LOG_ACESSO_COLMEIA e append-only por contrato: a aplicacao so insere e le.
    /// Autorizar um profissional a ler o historico do animal so e aceitavel se o
    /// Responsavel puder ver depois quem leu o que e quando — registro que pode ser
    /// apagado nao serve para auditar acesso.
    ///
    /// Nenhum indice unico aqui: o mesmo veterinario pode receber varias concessoes ao
    /// longo do tempo sobre o mesmo animal, e a vigencia e quem decide qual vale.
    /// </summary>
    public partial class AdicionaColmeia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_ACESSO_COLMEIA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    EMPRESA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    ESCOPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONCEDIDO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EXPIRA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    REVOGADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MOTIVO = table.Column<string>(type: "VARCHAR2(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_ACESSO_COLMEIA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_LOG_ACESSO_COLMEIA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ACESSO_COLMEIA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    ESCOPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ROTA = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: true),
                    PERMITIDO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    OCORRIDO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LOG_ACESSO_COLMEIA", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ACESSO_COLMEIA_ANIMAL_VET",
                table: "TB_ACESSO_COLMEIA",
                columns: new[] { "ANIMAL_ID", "VETERINARIO_ID" });

            migrationBuilder.CreateIndex(
                name: "IX_ACESSO_COLMEIA_EXPIRA_EM",
                table: "TB_ACESSO_COLMEIA",
                column: "EXPIRA_EM");

            migrationBuilder.CreateIndex(
                name: "IX_LOG_COLMEIA_ANIMAL_OCORRIDO",
                table: "TB_LOG_ACESSO_COLMEIA",
                columns: new[] { "ANIMAL_ID", "OCORRIDO_EM" });

            migrationBuilder.CreateIndex(
                name: "IX_LOG_COLMEIA_VETERINARIO",
                table: "TB_LOG_ACESSO_COLMEIA",
                column: "VETERINARIO_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_ACESSO_COLMEIA");

            migrationBuilder.DropTable(
                name: "TB_LOG_ACESSO_COLMEIA");
        }
    }
}
