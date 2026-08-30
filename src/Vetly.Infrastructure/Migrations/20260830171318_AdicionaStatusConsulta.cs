using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaStatusConsulta : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Migration ADITIVA COM BACKFILL. O estado da consulta era implicito em tres
        /// booleanos (CANCELADA, FINALIZADA, STATUS_PAGAMENTO), que nao expressam a maquina
        /// de estados da RN-035/RN-038 e nao distinguem no-show de cancelamento.
        ///
        /// As colunas antigas NAO sao removidas: seguem em dupla escrita por uma release,
        /// para nao quebrar o filtro ?cancelada= nem os testes existentes. A remocao vem em
        /// PR proprio, quando os consumidores tiverem migrado para STATUS.
        ///
        /// O backfill e deterministico e segue a precedencia da §7.2 do documento de
        /// engenharia: cancelada &gt; finalizada &gt; pagamento confirmado &gt; resto.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default 1 (EmCheckout) e nao 0: zero nao e membro valido de StatusConsulta.
            // O valor real de cada linha vem do backfill logo abaixo.
            migrationBuilder.AddColumn<int>(
                name: "STATUS",
                table: "TB_CONSULTA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1);

            // Backfill deterministico (§7.2):
            //   CANCELADA = 1                      -> 4 Cancelada
            //   FINALIZADA = 1                     -> 3 Realizada
            //   STATUS_PAGAMENTO = 2 (Confirmado)  -> 2 Confirmada
            //   demais (inclusive pagamento Pendente) -> 1 EmCheckout
            migrationBuilder.Sql(@"
                UPDATE TB_CONSULTA
                   SET STATUS = CASE
                       WHEN CANCELADA = 1        THEN 4
                       WHEN FINALIZADA = 1       THEN 3
                       WHEN STATUS_PAGAMENTO = 2 THEN 2
                       ELSE 1
                   END");

            // Indice criado depois do backfill: atualizar a tabela inteira com o indice ja
            // no lugar so daria trabalho a mais ao Oracle.
            migrationBuilder.CreateIndex(
                name: "IX_CONSULTA_STATUS",
                table: "TB_CONSULTA",
                column: "STATUS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CONSULTA_STATUS",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "STATUS",
                table: "TB_CONSULTA");
        }
    }
}
