using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaSplitPorPlanoNoPagamento : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Split por plano em TB_PAGAMENTO (RN-070/RN-071/RN-072), resolucao do C-01.
        ///
        /// Todas as colunas entram nullable e SEM backfill, de proposito: os pagamentos
        /// ja existentes foram repartidos pelo criterio antigo (persona), e reescrever
        /// valor financeiro passado com a regra nova seria falsificar historico. Eles
        /// mantem o PERCENTUAL_SPLIT que tinham; o split por plano vale das proximas
        /// transacoes em diante.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "COMISSAO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DESTINATARIO_REPASSE_ID",
                table: "TB_PAGAMENTO",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PLANO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "REPASSE",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TAKE_RATE",
                table: "TB_PAGAMENTO",
                type: "NUMBER(5,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "COMISSAO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "DESTINATARIO_REPASSE_ID",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "PLANO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "REPASSE",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "TAKE_RATE",
                table: "TB_PAGAMENTO");
        }
    }
}
