using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistraJustificativaDaCorrecao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JUSTIFICATIVA_CORRECAO",
                table: "TB_DOCUMENTO",
                type: "VARCHAR2(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JUSTIFICATIVA_CORRECAO",
                table: "TB_DOCUMENTO");
        }
    }
}
