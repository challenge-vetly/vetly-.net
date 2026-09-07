using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaDadosDeRepasseDoPrestador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AGENCIA_REPASSE",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(20)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BANCO_REPASSE",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(60)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CHAVE_PIX",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(140)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CONTA_REPASSE",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DOC_TITULAR_REPASSE",
                table: "TB_VETERINARIO",
                type: "VARCHAR2(14)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "REPASSE_ATUALIZADO_EM",
                table: "TB_VETERINARIO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AGENCIA_REPASSE",
                table: "TB_EMPRESA",
                type: "VARCHAR2(20)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BANCO_REPASSE",
                table: "TB_EMPRESA",
                type: "VARCHAR2(60)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CHAVE_PIX",
                table: "TB_EMPRESA",
                type: "VARCHAR2(140)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CONTA_REPASSE",
                table: "TB_EMPRESA",
                type: "VARCHAR2(30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DOC_TITULAR_REPASSE",
                table: "TB_EMPRESA",
                type: "VARCHAR2(14)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "REPASSE_ATUALIZADO_EM",
                table: "TB_EMPRESA",
                type: "TIMESTAMP(7)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AGENCIA_REPASSE",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "BANCO_REPASSE",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "CHAVE_PIX",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "CONTA_REPASSE",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "DOC_TITULAR_REPASSE",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "REPASSE_ATUALIZADO_EM",
                table: "TB_VETERINARIO");

            migrationBuilder.DropColumn(
                name: "AGENCIA_REPASSE",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "BANCO_REPASSE",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "CHAVE_PIX",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "CONTA_REPASSE",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "DOC_TITULAR_REPASSE",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "REPASSE_ATUALIZADO_EM",
                table: "TB_EMPRESA");
        }
    }
}
