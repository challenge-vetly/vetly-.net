using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// VALIDA e MOTIVO_INVALIDACAO em TB_AVALIACAO (RN-059).
    ///
    /// Aditiva — duas colunas novas, nenhuma existente e tocada.
    ///
    /// A RN-059 manda invalidar avaliacao de consulta cancelada ou reembolsada. O
    /// caminho escolhido e uma flag, e nao DELETE: a avaliacao sai do calculo da nota
    /// mas a linha permanece. Apagar registro de reputacao abriria caminho para
    /// gestao de nota via cancelamento — bastaria provocar o cancelamento para
    /// limpar uma avaliacao ruim, e a auditoria nao teria como notar.
    /// </summary>
    public partial class AdicionaValidadeDaAvaliacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MOTIVO_INVALIDACAO",
                table: "TB_AVALIACAO",
                type: "VARCHAR2(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VALIDA",
                table: "TB_AVALIACAO",
                type: "NUMBER(1)",
                nullable: false,
                // TRUE: toda avaliacao ja gravada era valida quando foi escrita, e a
                // invalidacao so acontece por cancelamento posterior. Default false
                // zeraria a reputacao de todos os profissionais da base.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MOTIVO_INVALIDACAO",
                table: "TB_AVALIACAO");

            migrationBuilder.DropColumn(
                name: "VALIDA",
                table: "TB_AVALIACAO");
        }
    }
}
