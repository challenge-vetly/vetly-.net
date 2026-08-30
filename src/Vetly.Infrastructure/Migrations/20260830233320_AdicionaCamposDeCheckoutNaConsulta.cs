using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCamposDeCheckoutNaConsulta : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Campos do checkout em TB_CONSULTA (RN-003/RN-032/RN-035/RN-040), aditivos.
        ///
        /// SLOT_ID, SERVICO_ID e EMPRESA_ID entram nullable: a emergencia presencial nao
        /// passa por slot nem por servico contratado (RN-040), e o autonomo nao tem empresa.
        ///
        /// ORIGEM recebe 2 (Emergencia) no backfill, e nao 0. Zero nao e membro valido do
        /// enum, e 2 e o valor CORRETO para as consultas existentes: todas foram criadas
        /// por POST /api/consultas, que e justamente a rota de emergencia e balcao — o
        /// checkout com lock so passa a existir agora.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EMPRESA_ID",
                table: "TB_CONSULTA",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ORIGEM",
                table: "TB_CONSULTA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 2);   // OrigemConsulta.Emergencia — ver remarks

            migrationBuilder.AddColumn<string>(
                name: "SERVICO_ID",
                table: "TB_CONSULTA",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SLOT_ID",
                table: "TB_CONSULTA",
                type: "CHAR(36)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EMPRESA_ID",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "ORIGEM",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "SERVICO_ID",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "SLOT_ID",
                table: "TB_CONSULTA");
        }
    }
}
