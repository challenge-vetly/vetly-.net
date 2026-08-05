using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase12_AssinaturaDocumento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ASSINATURA_NOME_DIGITADO",
                table: "TB_DOCUMENTO",
                type: "VARCHAR2(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_ASSINATURA",
                table: "TB_DOCUMENTO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HABILITA_DISPENSACAO_CONTROLADOS",
                table: "TB_DOCUMENTO",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TIPO_ASSINATURA",
                table: "TB_DOCUMENTO",
                type: "NUMBER(10)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ASSINATURA_NOME_DIGITADO",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "DATA_ASSINATURA",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "HABILITA_DISPENSACAO_CONTROLADOS",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "TIPO_ASSINATURA",
                table: "TB_DOCUMENTO");
        }
    }
}
