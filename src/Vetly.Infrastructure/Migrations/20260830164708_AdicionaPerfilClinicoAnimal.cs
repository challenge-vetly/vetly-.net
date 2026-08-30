using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaPerfilClinicoAnimal : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Migration ADITIVA: nenhuma coluna existente e alterada ou removida.
        /// PESO_KG entra nullable porque as linhas anteriores nao tem o dado; a API passa a
        /// exigi-lo na criacao do animal (RN-081).
        /// Os defaults das colunas de lista NAO sao string vazia: no Oracle '' E NULL, e uma
        /// coluna NOT NULL com default '' quebra o ALTER e os INSERTs seguintes. Por isso
        /// ALERGIAS/CONDICOES_PREEXISTENTES usam o sentinel ";" (mesmo de ALERTAS_ATIVOS) e
        /// CARTEIRA_VACINACAO usa "[]", exatamente o que os conversores gravam para lista vazia.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ALERGIAS",
                table: "TB_ANIMAL",
                type: "VARCHAR2(2000)",
                nullable: false,
                defaultValue: ";");

            migrationBuilder.AddColumn<string>(
                name: "CARTEIRA_VACINACAO",
                table: "TB_ANIMAL",
                type: "CLOB",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<bool>(
                name: "CASTRADO",
                table: "TB_ANIMAL",
                type: "NUMBER(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CONDICOES_PREEXISTENTES",
                table: "TB_ANIMAL",
                type: "VARCHAR2(2000)",
                nullable: false,
                defaultValue: ";");

            migrationBuilder.AddColumn<string>(
                name: "FOTO_MIDIA_ID",
                table: "TB_ANIMAL",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PESO_KG",
                table: "TB_ANIMAL",
                type: "NUMBER(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SEXO",
                table: "TB_ANIMAL",
                type: "NUMBER(10)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ALERGIAS",
                table: "TB_ANIMAL");

            migrationBuilder.DropColumn(
                name: "CARTEIRA_VACINACAO",
                table: "TB_ANIMAL");

            migrationBuilder.DropColumn(
                name: "CASTRADO",
                table: "TB_ANIMAL");

            migrationBuilder.DropColumn(
                name: "CONDICOES_PREEXISTENTES",
                table: "TB_ANIMAL");

            migrationBuilder.DropColumn(
                name: "FOTO_MIDIA_ID",
                table: "TB_ANIMAL");

            migrationBuilder.DropColumn(
                name: "PESO_KG",
                table: "TB_ANIMAL");

            migrationBuilder.DropColumn(
                name: "SEXO",
                table: "TB_ANIMAL");
        }
    }
}
