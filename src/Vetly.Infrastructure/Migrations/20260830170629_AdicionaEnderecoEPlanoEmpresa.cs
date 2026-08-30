using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaEnderecoEPlanoEmpresa : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Migration ADITIVA em TB_EMPRESA: endereco embutido + coordenada (RN-026),
        /// politica de retencao no cancelamento parcial (RN-042) e plano/faixa (RN-070/072).
        ///
        /// Backfill deliberado nos dois defaults gerados pelo EF:
        /// - PERCENTUAL_RETENCAO_PARCIAL: 0 -> 30. Zero nao seria "sem configuracao", seria
        ///   uma politica de retencao de 0%, ou seja, reembolso integral na faixa de 24h-2h.
        ///   30% e o padrao do onboarding que o codigo ja aplicava hardcoded (C-06).
        /// - PLANO: 0 -> 1 (Basico), porque 0 nao e membro valido de PlanoAssinatura.
        ///
        /// FAIXA_ENTERPRISE entra nula: so existe dentro do plano Enterprise e e recalculada
        /// a cada vinculacao de veterinario (RN-072).
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BAIRRO",
                table: "TB_EMPRESA",
                type: "VARCHAR2(150)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CEP",
                table: "TB_EMPRESA",
                type: "VARCHAR2(9)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CIDADE",
                table: "TB_EMPRESA",
                type: "VARCHAR2(150)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "COMPLEMENTO",
                table: "TB_EMPRESA",
                type: "VARCHAR2(100)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "COORDENADA_REVISAR",
                table: "TB_EMPRESA",
                type: "NUMBER(1)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FAIXA_ENTERPRISE",
                table: "TB_EMPRESA",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LATITUDE",
                table: "TB_EMPRESA",
                type: "NUMBER(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LOGRADOURO",
                table: "TB_EMPRESA",
                type: "VARCHAR2(200)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LONGITUDE",
                table: "TB_EMPRESA",
                type: "NUMBER(9,6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NUMERO",
                table: "TB_EMPRESA",
                type: "VARCHAR2(20)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PERCENTUAL_RETENCAO_PARCIAL",
                table: "TB_EMPRESA",
                type: "NUMBER(5,2)",
                nullable: false,
                defaultValue: 30m);   // padrao do onboarding (RN-042) — 0 daria reembolso integral na faixa parcial

            migrationBuilder.AddColumn<int>(
                name: "PLANO",
                table: "TB_EMPRESA",
                type: "NUMBER(10)",
                nullable: false,
                defaultValue: 1);   // PlanoAssinatura.Basico — 0 nao e membro valido

            migrationBuilder.AddColumn<string>(
                name: "UF",
                table: "TB_EMPRESA",
                type: "CHAR(2)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EMPRESA_COORDENADA",
                table: "TB_EMPRESA",
                columns: new[] { "LATITUDE", "LONGITUDE" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EMPRESA_COORDENADA",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "BAIRRO",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "CEP",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "CIDADE",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "COMPLEMENTO",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "COORDENADA_REVISAR",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "FAIXA_ENTERPRISE",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "LATITUDE",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "LOGRADOURO",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "LONGITUDE",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "NUMERO",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "PERCENTUAL_RETENCAO_PARCIAL",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "PLANO",
                table: "TB_EMPRESA");

            migrationBuilder.DropColumn(
                name: "UF",
                table: "TB_EMPRESA");
        }
    }
}
