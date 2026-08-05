using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase06_CancelamentoNoShowV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SUSPENSO_ATE",
                table: "TB_VETERINARIO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TB_VETERINARIO_STRIKE",
                columns: table => new
                {
                    Id = table.Column<int>(type: "NUMBER(10)", nullable: false)
                        .Annotation("Oracle:Identity", "START WITH 1 INCREMENT BY 1"),
                    DATA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    MOTIVO = table.Column<string>(type: "VARCHAR2(500)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_VETERINARIO_STRIKE", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TB_VETERINARIO_STRIKE_TB_VETERINARIO_VETERINARIO_ID",
                        column: x => x.VETERINARIO_ID,
                        principalTable: "TB_VETERINARIO",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TB_VETERINARIO_STRIKE_VETERINARIO_ID",
                table: "TB_VETERINARIO_STRIKE",
                column: "VETERINARIO_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_VETERINARIO_STRIKE");

            migrationBuilder.DropColumn(
                name: "SUSPENSO_ATE",
                table: "TB_VETERINARIO");
        }
    }
}
