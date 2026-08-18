using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase01_RenomeiaResponsavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename em vez de Drop/Create para preservar os dados já cadastrados
            // (a scaffold automática do EF propõe DropTable+CreateTable porque a classe
            // C# mudou de nome; RenameTable/AddColumn produz o mesmo modelo final sem perda).
            migrationBuilder.RenameTable(
                name: "TB_TUTOR",
                newName: "TB_RESPONSAVEL");

            migrationBuilder.RenameIndex(
                name: "IX_TUTOR_EMAIL",
                table: "TB_RESPONSAVEL",
                newName: "IX_RESPONSAVEL_EMAIL");

            migrationBuilder.AddColumn<int>(
                name: "TIER_FIDELIDADE",
                table: "TB_RESPONSAVEL",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1); // TierFidelidade.Bronze

            migrationBuilder.AddColumn<int>(
                name: "SALDO_PONTOS",
                table: "TB_RESPONSAVEL",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SALDO_CREDITOS_VETLY",
                table: "TB_RESPONSAVEL",
                type: "NUMBER(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte>(
                name: "CONTADOR_NO_SHOWS",
                table: "TB_RESPONSAVEL",
                type: "NUMBER(3)",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_ULTIMO_NO_SHOW",
                table: "TB_RESPONSAVEL",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BLOQUEADO_DESCONTOS_ATE",
                table: "TB_RESPONSAVEL",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "TUTOR_ID",
                table: "TB_PAGAMENTO",
                newName: "RESPONSAVEL_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PAGAMENTO_TUTOR",
                table: "TB_PAGAMENTO",
                newName: "IX_PAGAMENTO_RESPONSAVEL");

            migrationBuilder.RenameColumn(
                name: "TUTOR_RESPONDEU",
                table: "TB_LEMBRETE",
                newName: "RESPONSAVEL_RESPONDEU");

            migrationBuilder.RenameColumn(
                name: "TUTOR_ID",
                table: "TB_LEMBRETE",
                newName: "RESPONSAVEL_ID");

            migrationBuilder.RenameIndex(
                name: "IX_LEMBRETE_TUTOR",
                table: "TB_LEMBRETE",
                newName: "IX_LEMBRETE_RESPONSAVEL");

            migrationBuilder.RenameColumn(
                name: "LIBERADO_AO_TUTOR",
                table: "TB_EXAME",
                newName: "LIBERADO_AO_RESPONSAVEL");

            migrationBuilder.RenameColumn(
                name: "TUTOR_ID",
                table: "TB_CONSULTA",
                newName: "RESPONSAVEL_ID");

            migrationBuilder.RenameColumn(
                name: "TUTOR_ID",
                table: "TB_ANIMAL",
                newName: "RESPONSAVEL_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ANIMAL_TUTOR",
                table: "TB_ANIMAL",
                newName: "IX_ANIMAL_RESPONSAVEL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RESPONSAVEL_ID",
                table: "TB_PAGAMENTO",
                newName: "TUTOR_ID");

            migrationBuilder.RenameIndex(
                name: "IX_PAGAMENTO_RESPONSAVEL",
                table: "TB_PAGAMENTO",
                newName: "IX_PAGAMENTO_TUTOR");

            migrationBuilder.RenameColumn(
                name: "RESPONSAVEL_RESPONDEU",
                table: "TB_LEMBRETE",
                newName: "TUTOR_RESPONDEU");

            migrationBuilder.RenameColumn(
                name: "RESPONSAVEL_ID",
                table: "TB_LEMBRETE",
                newName: "TUTOR_ID");

            migrationBuilder.RenameIndex(
                name: "IX_LEMBRETE_RESPONSAVEL",
                table: "TB_LEMBRETE",
                newName: "IX_LEMBRETE_TUTOR");

            migrationBuilder.RenameColumn(
                name: "LIBERADO_AO_RESPONSAVEL",
                table: "TB_EXAME",
                newName: "LIBERADO_AO_TUTOR");

            migrationBuilder.RenameColumn(
                name: "RESPONSAVEL_ID",
                table: "TB_CONSULTA",
                newName: "TUTOR_ID");

            migrationBuilder.RenameColumn(
                name: "RESPONSAVEL_ID",
                table: "TB_ANIMAL",
                newName: "TUTOR_ID");

            migrationBuilder.RenameIndex(
                name: "IX_ANIMAL_RESPONSAVEL",
                table: "TB_ANIMAL",
                newName: "IX_ANIMAL_TUTOR");

            migrationBuilder.DropColumn(name: "BLOQUEADO_DESCONTOS_ATE", table: "TB_RESPONSAVEL");
            migrationBuilder.DropColumn(name: "DATA_ULTIMO_NO_SHOW", table: "TB_RESPONSAVEL");
            migrationBuilder.DropColumn(name: "CONTADOR_NO_SHOWS", table: "TB_RESPONSAVEL");
            migrationBuilder.DropColumn(name: "SALDO_CREDITOS_VETLY", table: "TB_RESPONSAVEL");
            migrationBuilder.DropColumn(name: "SALDO_PONTOS", table: "TB_RESPONSAVEL");
            migrationBuilder.DropColumn(name: "TIER_FIDELIDADE", table: "TB_RESPONSAVEL");

            migrationBuilder.RenameIndex(
                name: "IX_RESPONSAVEL_EMAIL",
                table: "TB_RESPONSAVEL",
                newName: "IX_TUTOR_EMAIL");

            migrationBuilder.RenameTable(
                name: "TB_RESPONSAVEL",
                newName: "TB_TUTOR");
        }
    }
}
