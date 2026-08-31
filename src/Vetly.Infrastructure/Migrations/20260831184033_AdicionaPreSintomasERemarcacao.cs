using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// Pre-sintomas e contador de remarcacoes em TB_CONSULTA (RN-036/RN-043).
    ///
    /// Aditiva — tres colunas novas, nenhuma existente e tocada.
    ///
    /// CONTADOR_REMARCACOES nasce em 0, que e o valor correto para as linhas
    /// existentes: nenhuma consulta anterior a esta migration foi remarcada pela
    /// plataforma, porque a rota nao existia.
    ///
    /// PRE_SINTOMAS e CLOB porque o texto guiado pode ser longo, e nullable porque
    /// consulta de emergencia (RN-040) nunca tem pre-sintoma — o animal chegou no
    /// balcao.
    /// </summary>
    public partial class AdicionaPreSintomasERemarcacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "CONTADOR_REMARCACOES",
                table: "TB_CONSULTA",
                type: "NUMBER(2)",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "PRE_SINTOMAS",
                table: "TB_CONSULTA",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PRE_SINTOMAS_MIDIAS",
                table: "TB_CONSULTA",
                type: "VARCHAR2(2000)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CONTADOR_REMARCACOES",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "PRE_SINTOMAS",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "PRE_SINTOMAS_MIDIAS",
                table: "TB_CONSULTA");
        }
    }
}
