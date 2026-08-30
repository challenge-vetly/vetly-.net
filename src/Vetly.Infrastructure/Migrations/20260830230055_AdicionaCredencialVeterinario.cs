using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCredencialVeterinario : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Credencial de acesso do veterinario (§2.2). Ate aqui so o Responsavel tinha
        /// login proprio; o vet dependia da rota de token de desenvolvimento.
        ///
        /// EMAIL e SENHA_HASH entram nullable: os cadastros anteriores nao tem credencial
        /// e nao ha como inventar uma. O indice unico em EMAIL nao atrapalha esses casos —
        /// no Oracle, NULL nao participa de indice unico, entao varios vets sem e-mail
        /// convivem sem conflito.
        ///
        /// SENHA_TEMPORARIA marca a senha gerada pelo Admin no cadastro, que o profissional
        /// precisa trocar no primeiro acesso. E o caminho conservador que a propria
        /// pendencia P-05 sugere, ja que nao ha servico de e-mail no projeto para convite.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EMAIL",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SENHA_HASH",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SENHA_TEMPORARIA",
                table: "TB_VETERINARIO",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_VETERINARIO_EMAIL",
                table: "TB_VETERINARIO",
                column: "EMAIL",
                unique: true,
                filter: "\"EMAIL\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VETERINARIO_EMAIL",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "EMAIL",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "SENHA_HASH",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "SENHA_TEMPORARIA",
                table: "TB_VETERINARIO");
        }
    }
}
