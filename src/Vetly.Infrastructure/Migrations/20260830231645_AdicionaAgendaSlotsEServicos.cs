using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAgendaSlotsEServicos : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Fundacao da agenda (onda 3): TB_AGENDA_CONFIG, TB_SLOT e TB_SERVICO.
        /// Tres tabelas novas, nenhuma coluna existente tocada.
        ///
        /// O slot e o que impede overbooking sem gateway real (RN-035): a disponibilidade
        /// nao e calculada em tempo de consulta, e linha no banco, para que o checkout
        /// tenha o que travar. O indice unico (VETERINARIO_ID, INICIO) garante que
        /// materializar a agenda duas vezes nao duplique horario.
        ///
        /// TB_SERVICO e o que da preco a consulta (RN-032): ate aqui o valor vinha solto
        /// no pagamento, sem nada que o justificasse.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_AGENDA_CONFIG",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    DIAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    INICIO_EM_MINUTOS = table.Column<byte>(type: "NUMBER(4)", nullable: false),
                    FIM_EM_MINUTOS = table.Column<byte>(type: "NUMBER(4)", nullable: false),
                    DURACAO_MINUTOS = table.Column<byte>(type: "NUMBER(4)", nullable: false),
                    INTERVALO_MINUTOS = table.Column<byte>(type: "NUMBER(4)", nullable: false),
                    ATUALIZADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_AGENDA_CONFIG", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_SERVICO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    PRESTADOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VALOR = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    ACEITA_PLANO_PET = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DURACAO_MINUTOS = table.Column<byte>(type: "NUMBER(4)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_SERVICO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_SLOT",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    INICIO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    FIM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ESTADO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    LOCK_ATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LOCK_CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_SLOT", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AGENDA_CONFIG_VETERINARIO",
                table: "TB_AGENDA_CONFIG",
                column: "VETERINARIO_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SERVICO_PRESTADOR_TIPO",
                table: "TB_SERVICO",
                columns: new[] { "PRESTADOR_ID", "TIPO" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SLOT_ESTADO_INICIO",
                table: "TB_SLOT",
                columns: new[] { "ESTADO", "INICIO" });

            migrationBuilder.CreateIndex(
                name: "IX_SLOT_VETERINARIO_INICIO",
                table: "TB_SLOT",
                columns: new[] { "VETERINARIO_ID", "INICIO" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_AGENDA_CONFIG");

            migrationBuilder.DropTable(
                name: "TB_SERVICO");

            migrationBuilder.DropTable(
                name: "TB_SLOT");
        }
    }
}
