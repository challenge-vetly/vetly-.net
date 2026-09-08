using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeTruncamentoDeInteirosEmByte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "DURACAO_MINUTOS",
                table: "TB_SERVICO",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "NUMBER(4)");

            migrationBuilder.AlterColumn<int>(
                name: "STATUS_HTTP",
                table: "TB_IDEMPOTENCIA",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "NUMBER(3)");

            migrationBuilder.AlterColumn<int>(
                name: "INTERVALO_MINUTOS",
                table: "TB_AGENDA_CONFIG",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "NUMBER(4)");

            migrationBuilder.AlterColumn<int>(
                name: "INICIO_EM_MINUTOS",
                table: "TB_AGENDA_CONFIG",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "NUMBER(4)");

            migrationBuilder.AlterColumn<int>(
                name: "FIM_EM_MINUTOS",
                table: "TB_AGENDA_CONFIG",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "NUMBER(4)");

            migrationBuilder.AlterColumn<int>(
                name: "DURACAO_MINUTOS",
                table: "TB_AGENDA_CONFIG",
                type: "NUMBER(10)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "NUMBER(4)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte>(
                name: "DURACAO_MINUTOS",
                table: "TB_SERVICO",
                type: "NUMBER(4)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<byte>(
                name: "STATUS_HTTP",
                table: "TB_IDEMPOTENCIA",
                type: "NUMBER(3)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<byte>(
                name: "INTERVALO_MINUTOS",
                table: "TB_AGENDA_CONFIG",
                type: "NUMBER(4)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<byte>(
                name: "INICIO_EM_MINUTOS",
                table: "TB_AGENDA_CONFIG",
                type: "NUMBER(4)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<byte>(
                name: "FIM_EM_MINUTOS",
                table: "TB_AGENDA_CONFIG",
                type: "NUMBER(4)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");

            migrationBuilder.AlterColumn<byte>(
                name: "DURACAO_MINUTOS",
                table: "TB_AGENDA_CONFIG",
                type: "NUMBER(4)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "NUMBER(10)");
        }
    }
}
