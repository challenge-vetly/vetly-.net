using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistraDadosDoAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ULTIMO_CUMPRIMENTO_NO_PRAZO",
                table: "TB_OBRIGACAO_PET",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PESO_REGISTRADO_EM",
                table: "TB_ANIMAL",
                type: "TIMESTAMP(7)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ULTIMO_CUMPRIMENTO_NO_PRAZO",
                table: "TB_OBRIGACAO_PET");

            migrationBuilder.DropColumn(
                name: "PESO_REGISTRADO_EM",
                table: "TB_ANIMAL");
        }
    }
}
