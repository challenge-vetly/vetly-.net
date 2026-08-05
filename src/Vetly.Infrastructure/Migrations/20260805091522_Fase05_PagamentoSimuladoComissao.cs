using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase05_PagamentoSimuladoComissao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DESCONTO_FIDELIDADE_CALCULADO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "INCIDENCIA_VETERINARIO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "INCIDENCIA_VETLY",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PERCENTUAL_COMISSAO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "SIMULADO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VALOR_COMISSAO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "VALOR_REPASSE",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DESCONTO_FIDELIDADE_CALCULADO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "INCIDENCIA_VETERINARIO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "INCIDENCIA_VETLY",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "PERCENTUAL_COMISSAO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "SIMULADO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "VALOR_COMISSAO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "VALOR_REPASSE",
                table: "TB_PAGAMENTO");
        }
    }
}
