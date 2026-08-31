using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// TB_MOVIMENTO_PONTOS e as colunas de resgate em TB_PAGAMENTO (RN-051/RN-052).
    ///
    /// Aditiva — tabela nova e duas colunas novas, nullable; nenhuma coluna existente
    /// e tocada.
    ///
    /// TB_MOVIMENTO_PONTOS e append-only e nao tem coluna de saldo: o saldo e a soma
    /// de PONTOS por TUTOR_ID. Saldo guardado a parte diverge do extrato no primeiro
    /// erro, e ai nao ha como saber qual dos dois esta certo. PONTOS e assinado —
    /// negativo em debito e expiracao — justamente para que a soma feche.
    ///
    /// Tambem nao ha coluna de "ja expirado": a baixa e um lancamento de expiracao que
    /// aponta para o credito em MOVIMENTO_ORIGEM_ID, e a ausencia dele e o que marca o
    /// que falta processar. Coluna de estado numa tabela append-only seria uma
    /// contradicao.
    ///
    /// VALOR_DO_DESCONTO em TB_PAGAMENTO e coluna propria, e nao um VALOR ja reduzido:
    /// o bruto continua sendo o preco do servico, e e sobre ele que o repasse ao
    /// prestador foi calculado. Guardar so o liquido apagaria de quem saiu o dinheiro
    /// — que e a pergunta central da RN-051.
    /// </summary>
    public partial class AdicionaFidelidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PONTOS_RESGATADOS",
                table: "TB_PAGAMENTO",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VALOR_DO_DESCONTO",
                table: "TB_PAGAMENTO",
                type: "NUMBER(18,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TB_MOVIMENTO_PONTOS",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PONTOS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    PAGAMENTO_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    VALOR_EM_REAIS = table.Column<decimal>(type: "NUMBER(18,2)", nullable: true),
                    EXPIRA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    MOVIMENTO_ORIGEM_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    DESCRICAO = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: true),
                    OCORRIDO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_MOVIMENTO_PONTOS", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PONTOS_CONSULTA",
                table: "TB_MOVIMENTO_PONTOS",
                column: "CONSULTA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PONTOS_EXPIRA_EM",
                table: "TB_MOVIMENTO_PONTOS",
                column: "EXPIRA_EM");

            migrationBuilder.CreateIndex(
                name: "IX_PONTOS_TUTOR_OCORRIDO",
                table: "TB_MOVIMENTO_PONTOS",
                columns: new[] { "TUTOR_ID", "OCORRIDO_EM" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_MOVIMENTO_PONTOS");

            migrationBuilder.DropColumn(
                name: "PONTOS_RESGATADOS",
                table: "TB_PAGAMENTO");

            migrationBuilder.DropColumn(
                name: "VALOR_DO_DESCONTO",
                table: "TB_PAGAMENTO");
        }
    }
}
