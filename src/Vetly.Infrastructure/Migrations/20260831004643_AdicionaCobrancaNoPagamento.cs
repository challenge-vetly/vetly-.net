using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCobrancaNoPagamento : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Cobranca em TB_PAGAMENTO (RN-006/RN-071, §5.1): TIPO, REFERENCIA_EXTERNA,
        /// CHAVE_IDEMPOTENCIA e LIQUIDADO. Aditiva.
        ///
        /// LIQUIDADO nasce 0 e assim permanece no MVP: valores sao apurados e
        /// registrados, nunca repassados (RN-071).
        ///
        /// REFERENCIA_EXTERNA e CHAVE_IDEMPOTENCIA ficam nulas nos pagamentos antigos —
        /// eles nunca passaram por adaptador, e inventar referencia de cobranca que
        /// nunca existiu seria pior que deixar nulo.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CHAVE_IDEMPOTENCIA",
                table: "TB_PAGAMENTO",
                type: "VARCHAR2(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LIQUIDADO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "REFERENCIA_EXTERNA",
                table: "TB_PAGAMENTO",
                type: "VARCHAR2(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TIPO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1);   // TipoPagamento.Consulta — ver o backfill abaixo

            migrationBuilder.CreateIndex(
                name: "IX_PAGAMENTO_REFERENCIA",
                table: "TB_PAGAMENTO",
                column: "REFERENCIA_EXTERNA");

            // Backfill do TIPO: pagamento com INTERNACAO_ID e caucao de internacao (RN-101);
            // os demais sao de consulta. Zero nao e membro valido do enum.
            migrationBuilder.Sql(@"
                UPDATE TB_PAGAMENTO
                   SET TIPO = CASE WHEN INTERNACAO_ID IS NOT NULL THEN 2 ELSE 1 END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PAGAMENTO_REFERENCIA",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "CHAVE_IDEMPOTENCIA",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "LIQUIDADO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "REFERENCIA_EXTERNA",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "TIPO",
                table: "TB_PAGAMENTO");
        }
    }
}
