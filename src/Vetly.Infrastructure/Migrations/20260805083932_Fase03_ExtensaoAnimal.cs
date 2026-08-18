using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase03_ExtensaoAnimal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ALERTA_SEGURANCA",
                table: "TB_PRONTUARIO",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ALERGIAS",
                table: "TB_ANIMAL",
                type: "VARCHAR2(2000)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CARTEIRA_VACINACAO",
                table: "TB_ANIMAL",
                type: "VARCHAR2(2000)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CASTRADO",
                table: "TB_ANIMAL",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CONDICOES_PREEXISTENTES",
                table: "TB_ANIMAL",
                type: "VARCHAR2(2000)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FOTO_URL",
                table: "TB_ANIMAL",
                type: "VARCHAR2(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MEDICACOES_EM_USO",
                table: "TB_ANIMAL",
                type: "VARCHAR2(2000)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PESO_KG",
                table: "TB_ANIMAL",
                type: "NUMBER(6,2)",
                nullable: true);

            // Default 1 = SexoAnimal.Macho — mantém as linhas pré-existentes com um valor
            // de enum válido (0 não corresponde a nenhum membro de SexoAnimal).
            migrationBuilder.AddColumn<int>(
                name: "SEXO",
                table: "TB_ANIMAL",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "TB_REGISTRO_OCULTADO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    PRONTUARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    DATA_OCULTACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_REGISTRO_OCULTADO", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_REGISTRO_OCULTADO_ANIMAL_PRONTUARIO",
                table: "TB_REGISTRO_OCULTADO",
                columns: new[] { "ANIMAL_ID", "PRONTUARIO_ID" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_REGISTRO_OCULTADO");

            migrationBuilder.DropColumn(
                name: "ALERTA_SEGURANCA",
                table: "TB_PRONTUARIO");

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
                name: "FOTO_URL",
                table: "TB_ANIMAL");

            migrationBuilder.DropColumn(
                name: "MEDICACOES_EM_USO",
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
