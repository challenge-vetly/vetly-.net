using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// MIDIA_IDS em TB_EXAME (RN-104).
    ///
    /// Aditiva — uma coluna nova, nullable; nenhuma existente e tocada.
    ///
    /// Resultado de exame raramente e so texto: o laudo vem em PDF e a imagem vem do
    /// equipamento. Sem esta coluna, o veterinario teria de transcrever o que ja
    /// existe em arquivo — e a transcricao e onde o dado se perde.
    ///
    /// Lista separada por ";", o mesmo padrao ja usado em ALERGIAS e
    /// PRE_SINTOMAS_MIDIAS. Nullable porque exame sem resultado ainda nao tem laudo.
    /// </summary>
    public partial class AdicionaMidiasDoExame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MIDIA_IDS",
                table: "TB_EXAME",
                type: "VARCHAR2(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MIDIA_IDS",
                table: "TB_EXAME");
        }
    }
}
