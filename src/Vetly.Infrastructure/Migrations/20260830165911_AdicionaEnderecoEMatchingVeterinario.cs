using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaEnderecoEMatchingVeterinario : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Migration ADITIVA em TB_VETERINARIO: endereco embutido + coordenada (RN-026),
        /// status do CRMV junto ao conselho (RN-107) e metricas de matching (RN-030/033/057).
        ///
        /// Backfill dos enums feito pelos defaults, porque 0 nao e membro valido de nenhum
        /// dos dois: CRMV_STATUS = 1 (PendenteValidacao) e MATCHING_STATUS = 1 (Ativo).
        /// Deixar os veterinarios existentes como PendenteValidacao e deliberado — a RN-107
        /// diz que perfil nao validado nao e publicado, e nunca se aprova por omissao.
        /// Como PUBLICADO nasce 0 e a busca so entra na onda 3, nada deixa de funcionar hoje.
        ///
        /// As colunas de endereco entram todas nullable: os cadastros anteriores nao tem
        /// endereco. CEP e a propriedade que marca a existencia do dependente opcional.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BAIRRO",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(150)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CEP",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(9)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CIDADE",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(150)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "COMPLEMENTO",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(100)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "COORDENADA_REVISAR",
                table: "TB_VETERINARIO",
                type: "NUMBER(1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CRMV_STATUS",
                table: "TB_VETERINARIO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1);   // StatusCrmv.PendenteValidacao — nunca aprovar por omissao (RN-107)

            migrationBuilder.AddColumn<DateTime>(
                name: "CRMV_VALIDADO_EM",
                table: "TB_VETERINARIO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LATITUDE",
                table: "TB_VETERINARIO",
                type: "NUMBER(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LOGRADOURO",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(200)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LONGITUDE",
                table: "TB_VETERINARIO",
                type: "NUMBER(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MATCHING_STATUS",
                table: "TB_VETERINARIO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1);   // StatusMatching.Ativo

            migrationBuilder.AddColumn<decimal>(
                name: "NOTA_MEDIA",
                table: "TB_VETERINARIO",
                type: "NUMBER(3,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "NUMERO",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(20)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NUM_AVALIACOES",
                table: "TB_VETERINARIO",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PUBLICADO",
                table: "TB_VETERINARIO",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PUBLICADO_EM",
                table: "TB_VETERINARIO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UF",
                table: "TB_VETERINARIO",
                type: "CHAR(2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VETERINARIO_COORDENADA",
                table: "TB_VETERINARIO",
                columns: new[] { "LATITUDE", "LONGITUDE" });

            migrationBuilder.CreateIndex(
                name: "IX_VETERINARIO_MATCHING",
                table: "TB_VETERINARIO",
                columns: new[] { "PUBLICADO", "MATCHING_STATUS", "CRMV_STATUS" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VETERINARIO_COORDENADA",
                table: "TB_VETERINARIO");

            migrationBuilder.DropIndex(
                name: "IX_VETERINARIO_MATCHING",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "BAIRRO",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "CEP",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "CIDADE",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "COMPLEMENTO",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "COORDENADA_REVISAR",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "CRMV_STATUS",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "CRMV_VALIDADO_EM",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "LATITUDE",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "LOGRADOURO",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "LONGITUDE",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "MATCHING_STATUS",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "NOTA_MEDIA",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "NUMERO",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "NUM_AVALIACOES",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "PUBLICADO",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "PUBLICADO_EM",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "UF",
                table: "TB_VETERINARIO");
        }
    }
}
