using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// Alinha a fidelidade aos parametros fechados do vetly-tech §1 (RN-047 a RN-054).
    ///
    /// Aditiva — colunas novas e uma tabela nova; nenhuma coluna existente e removida.
    ///
    /// O que estava errado antes desta migration:
    ///
    ///   - a conversao era 100 pontos = R$ 1,00; a RN-049 fecha em R$ 3,00;
    ///   - nao havia tier nem multiplicador (RN-048), entao todo credito valia 1,0x;
    ///   - nao havia bonus por obrigacao cumprida (RN-047), so por gasto;
    ///   - nao havia consumo FIFO (RN-050): faltava o saldo por lote;
    ///   - nao havia cupom (RN-053/RN-054): o desconto era aplicado direto na cobranca;
    ///   - o desconto saia inteiro da comissao, contrariando as faixas da RN-051.
    ///
    /// RESTANTE e o mecanismo do FIFO: e a unica coluna do extrato que muda depois de
    /// gravada, e muda so para menos. O valor do lancamento continua imutavel.
    ///
    /// Em TB_PAGAMENTO, DESCONTO_VETLY e DESCONTO_PRESTADOR sao colunas separadas
    /// porque saem de bolsos diferentes (RN-051). Guardar so o total esconderia quem
    /// pagou pela promocao, que e justamente a pergunta que a regra responde.
    /// </summary>
    public partial class CorrigeFidelidadeParaRegrasFechadas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CUPOM_ID",
                table: "TB_PAGAMENTO",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DESCONTO_PRESTADOR",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DESCONTO_VETLY",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FAIXA_DESCONTO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CUPOM_ID",
                table: "TB_MOVIMENTO_PONTOS",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MULTIPLICADOR",
                table: "TB_MOVIMENTO_PONTOS",
                type: "NUMBER(4,2)",
                nullable: false,
                // 1,00 = Bronze. Zero nao e multiplicador valido, e todo credito
                // anterior a esta migration foi lancado sem multiplicador.
                defaultValue: 1.00m);

            migrationBuilder.AddColumn<string>(
                name: "OBRIGACAO_ID",
                table: "TB_MOVIMENTO_PONTOS",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PONTOS_BRUTOS",
                table: "TB_MOVIMENTO_PONTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RESTANTE",
                table: "TB_MOVIMENTO_PONTOS",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            // ── Backfill (§4.3) ──────────────────────────────────────────────
            //
            // PONTOS_BRUTOS e RESTANTE nascem com default 0, que esta errado para as
            // linhas que ja existem:
            //
            //   PONTOS_BRUTOS  antes desta migration nao havia multiplicador, entao o
            //                  bruto e igual ao creditado.
            //   RESTANTE       e o saldo do lote no consumo FIFO. Deixar zero apagaria
            //                  o saldo de todo credito ja lancado — o Responsavel
            //                  perderia pontos que conquistou.
            //
            // Debito, estorno e expiracao continuam com RESTANTE zero: nao sao lotes,
            // e nao ha o que consumir deles.
            migrationBuilder.Sql(@"
                UPDATE TB_MOVIMENTO_PONTOS
                   SET PONTOS_BRUTOS = PONTOS,
                       RESTANTE      = PONTOS
                 WHERE TIPO = 1");

            migrationBuilder.CreateTable(
                name: "TB_CUPOM_RESGATE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CODIGO_QR = table.Column<string>(type: "VARCHAR2(40)", maxLength: 40, nullable: false),
                    ITEM_REF = table.Column<string>(type: "VARCHAR2(120)", maxLength: 120, nullable: false),
                    ITEM_NOME = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: true),
                    ITEM_CATEGORIA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PONTOS_DEBITADOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DESCONTO = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    FAIXA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DESCONTO_VETLY = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    DESCONTO_PRESTADOR = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    EMITIDO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EXPIRA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    RESGATADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_CUPOM_RESGATE", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PONTOS_OBRIGACAO",
                table: "TB_MOVIMENTO_PONTOS",
                column: "OBRIGACAO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CUPOM_CODIGO",
                table: "TB_CUPOM_RESGATE",
                column: "CODIGO_QR",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CUPOM_STATUS_EXPIRA",
                table: "TB_CUPOM_RESGATE",
                columns: new[] { "STATUS", "EXPIRA_EM" });

            migrationBuilder.CreateIndex(
                name: "IX_CUPOM_TUTOR",
                table: "TB_CUPOM_RESGATE",
                columns: new[] { "TUTOR_ID", "EMITIDO_EM" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_CUPOM_RESGATE");

            migrationBuilder.DropIndex(
                name: "IX_PONTOS_OBRIGACAO",
                table: "TB_MOVIMENTO_PONTOS");

            migrationBuilder.DropColumn(
                name: "CUPOM_ID",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "DESCONTO_PRESTADOR",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "DESCONTO_VETLY",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "FAIXA_DESCONTO",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "CUPOM_ID",
                table: "TB_MOVIMENTO_PONTOS");

            migrationBuilder.DropColumn(
                name: "MULTIPLICADOR",
                table: "TB_MOVIMENTO_PONTOS");

            migrationBuilder.DropColumn(
                name: "OBRIGACAO_ID",
                table: "TB_MOVIMENTO_PONTOS");

            migrationBuilder.DropColumn(
                name: "PONTOS_BRUTOS",
                table: "TB_MOVIMENTO_PONTOS");

            migrationBuilder.DropColumn(
                name: "RESTANTE",
                table: "TB_MOVIMENTO_PONTOS");
        }
    }
}
