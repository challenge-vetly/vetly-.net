using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase04_MaquinaEstadosConsulta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A scaffold automática do EF tentou "renomear" STATUS_PAGAMENTO para TIPO_SERVICO
            // por coincidência de forma (ambas NUMBER(10)) — são enums completamente diferentes
            // (StatusPagamento vs. TipoServico); por isso este arquivo foi reescrito à mão como
            // Add/Drop separados, com backfill explícito de STATUS a partir dos campos antigos.
            migrationBuilder.AddColumn<int>(
                name: "STATUS",
                table: "TB_CONSULTA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1); // StatusConsulta.EmCheckout

            migrationBuilder.AddColumn<int>(
                name: "TIPO_SERVICO",
                table: "TB_CONSULTA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1); // TipoServico.Consulta — sem equivalente no v1, default razoável

            migrationBuilder.AddColumn<short>(
                name: "CONTADOR_REMARCACOES",
                table: "TB_CONSULTA",
                type: "NUMBER(5)",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_REALIZADA",
                table: "TB_CONSULTA",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LOCK_CHECKOUT_EXPIRA_EM",
                table: "TB_CONSULTA",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PRE_SINTOMAS",
                table: "TB_CONSULTA",
                type: "VARCHAR2(4000)",
                maxLength: 4000,
                nullable: true);

            // Backfill de STATUS a partir dos campos que serão removidos (nesta ordem de
            // prioridade): CANCELADA=1 → Cancelada; senão FINALIZADA=1 → Realizada (a data
            // exata não é recuperável, DATA_REALIZADA fica nula); senão STATUS_PAGAMENTO=2
            // (Confirmado) → Confirmada; caso contrário permanece EmCheckout (default acima).
            migrationBuilder.Sql(@"
                UPDATE TB_CONSULTA SET STATUS = 4 WHERE CANCELADA = 1");

            migrationBuilder.Sql(@"
                UPDATE TB_CONSULTA SET STATUS = 3 WHERE CANCELADA = 0 AND FINALIZADA = 1");

            migrationBuilder.Sql(@"
                UPDATE TB_CONSULTA SET STATUS = 2
                WHERE CANCELADA = 0 AND FINALIZADA = 0 AND STATUS_PAGAMENTO = 2");

            migrationBuilder.DropColumn(
                name: "CANCELADA",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "FINALIZADA",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "STATUS_PAGAMENTO",
                table: "TB_CONSULTA");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CANCELADA",
                table: "TB_CONSULTA",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FINALIZADA",
                table: "TB_CONSULTA",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "STATUS_PAGAMENTO",
                table: "TB_CONSULTA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1); // StatusPagamento.Pendente

            // Melhor esforço: reconstrói os campos v1 a partir de STATUS. NoShow* não tem
            // equivalente no v1 — cai em "não cancelada, não finalizada" (StatusPagamento
            // permanece o default acima), a única aproximação possível sem inventar semântica.
            migrationBuilder.Sql(@"
                UPDATE TB_CONSULTA SET CANCELADA = 1 WHERE STATUS = 4");

            migrationBuilder.Sql(@"
                UPDATE TB_CONSULTA SET FINALIZADA = 1 WHERE STATUS = 3");

            migrationBuilder.Sql(@"
                UPDATE TB_CONSULTA SET STATUS_PAGAMENTO = 2 WHERE STATUS IN (2, 3)");

            migrationBuilder.DropColumn(
                name: "STATUS",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "TIPO_SERVICO",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "CONTADOR_REMARCACOES",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "DATA_REALIZADA",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "LOCK_CHECKOUT_EXPIRA_EM",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "PRE_SINTOMAS",
                table: "TB_CONSULTA");
        }
    }
}
