using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaListaDeEspera : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Lista de espera (RN-004/RN-037). Tabela nova, nenhuma existente tocada.
        ///
        /// Existe para o caso em que nao ha horario: em vez de perder a demanda, a
        /// plataforma guarda a intencao e avisa quando abrir vaga. A ordem da fila e a
        /// data de entrada, e o indice (VETERINARIO_ID, ESTADO, CRIADO_EM) e exatamente
        /// a leitura que a promocao faz.
        ///
        /// A entidade nao existe no mapa do vetly-tech — e a pendencia P-03, que pede
        /// para adiciona-la ao mapa de entidades.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_LISTA_ESPERA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    NECESSIDADE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ESTADO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    SLOT_OFERECIDO_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    PRIORIDADE_ATE = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LISTA_ESPERA", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LISTA_ESPERA_FILA",
                table: "TB_LISTA_ESPERA",
                columns: new[] { "VETERINARIO_ID", "ESTADO", "CRIADO_EM" });

            migrationBuilder.CreateIndex(
                name: "IX_LISTA_ESPERA_TUTOR",
                table: "TB_LISTA_ESPERA",
                column: "TUTOR_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_LISTA_ESPERA");
        }
    }
}
