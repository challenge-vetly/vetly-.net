using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fase02_ConsentimentoLgpd : Migration
    {
        // Formata um RAW(16) do SYS_GUID() como string "8-4-4-4-12" (mesmo formato de
        // Guid.ToString() usado pelo restante do schema, ex. "d3f4d2a1-....-....-....-............").
        private const string GerarGuidSql =
            "LOWER(REGEXP_REPLACE(RAWTOHEX(SYS_GUID()), '(.{8})(.{4})(.{4})(.{4})(.{12})', '\\1-\\2-\\3-\\4-\\5'))";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_CONSENTIMENTO_LGPD",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    RESPONSAVEL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    FINALIDADE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CONCEDIDO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DATA_CONCESSAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DATA_REVOGACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_CONSENTIMENTO_LGPD", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CONSENTIMENTO_RESPONSAVEL_FINALIDADE",
                table: "TB_CONSENTIMENTO_LGPD",
                columns: new[] { "RESPONSAVEL_ID", "FINALIDADE" });

            // Migra dados: cada flag booleana antiga (ainda presente em TB_RESPONSAVEL neste
            // ponto da migration) vira um registro de consentimento concedido, usando
            // DATA_CONSENTIMENTO como DATA_CONCESSAO (ou o momento da migração, se nula).
            // FINALIDADE: 1=AtendimentoClinico, 2=LembretesComunicacao, 3=CompartilhamentoRede.
            migrationBuilder.Sql($@"
                INSERT INTO TB_CONSENTIMENTO_LGPD (ID, RESPONSAVEL_ID, FINALIDADE, CONCEDIDO, DATA_CONCESSAO, DATA_REVOGACAO)
                SELECT {GerarGuidSql}, ID, 1, 1, NVL(DATA_CONSENTIMENTO, SYSTIMESTAMP), NULL
                FROM TB_RESPONSAVEL
                WHERE CONSENTIMENTO_ATENDIMENTO = 1");

            migrationBuilder.Sql($@"
                INSERT INTO TB_CONSENTIMENTO_LGPD (ID, RESPONSAVEL_ID, FINALIDADE, CONCEDIDO, DATA_CONCESSAO, DATA_REVOGACAO)
                SELECT {GerarGuidSql}, ID, 2, 1, NVL(DATA_CONSENTIMENTO, SYSTIMESTAMP), NULL
                FROM TB_RESPONSAVEL
                WHERE CONSENTIMENTO_LEMBRETES = 1");

            migrationBuilder.Sql($@"
                INSERT INTO TB_CONSENTIMENTO_LGPD (ID, RESPONSAVEL_ID, FINALIDADE, CONCEDIDO, DATA_CONCESSAO, DATA_REVOGACAO)
                SELECT {GerarGuidSql}, ID, 3, 1, NVL(DATA_CONSENTIMENTO, SYSTIMESTAMP), NULL
                FROM TB_RESPONSAVEL
                WHERE CONSENTIMENTO_COMPARTILHAMENTO = 1");

            migrationBuilder.DropColumn(
                name: "CONSENTIMENTO_ATENDIMENTO",
                table: "TB_RESPONSAVEL");

            migrationBuilder.DropColumn(
                name: "CONSENTIMENTO_COMPARTILHAMENTO",
                table: "TB_RESPONSAVEL");

            migrationBuilder.DropColumn(
                name: "CONSENTIMENTO_LEMBRETES",
                table: "TB_RESPONSAVEL");

            migrationBuilder.DropColumn(
                name: "DATA_CONSENTIMENTO",
                table: "TB_RESPONSAVEL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CONSENTIMENTO_ATENDIMENTO",
                table: "TB_RESPONSAVEL",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CONSENTIMENTO_COMPARTILHAMENTO",
                table: "TB_RESPONSAVEL",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CONSENTIMENTO_LEMBRETES",
                table: "TB_RESPONSAVEL",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_CONSENTIMENTO",
                table: "TB_RESPONSAVEL",
                type: "TIMESTAMP(7)",
                nullable: true);

            // Melhor esforço: restaura os booleanos a partir do consentimento ATIVO mais
            // recente de cada finalidade. O histórico de revogações — que só existe no
            // modelo v2 — é necessariamente perdido ao reverter para o modelo v1.
            migrationBuilder.Sql(@"
                UPDATE TB_RESPONSAVEL r SET
                    CONSENTIMENTO_ATENDIMENTO = CASE WHEN EXISTS (
                        SELECT 1 FROM TB_CONSENTIMENTO_LGPD c
                        WHERE c.RESPONSAVEL_ID = r.ID AND c.FINALIDADE = 1 AND c.DATA_REVOGACAO IS NULL
                    ) THEN 1 ELSE 0 END,
                    CONSENTIMENTO_LEMBRETES = CASE WHEN EXISTS (
                        SELECT 1 FROM TB_CONSENTIMENTO_LGPD c
                        WHERE c.RESPONSAVEL_ID = r.ID AND c.FINALIDADE = 2 AND c.DATA_REVOGACAO IS NULL
                    ) THEN 1 ELSE 0 END,
                    CONSENTIMENTO_COMPARTILHAMENTO = CASE WHEN EXISTS (
                        SELECT 1 FROM TB_CONSENTIMENTO_LGPD c
                        WHERE c.RESPONSAVEL_ID = r.ID AND c.FINALIDADE = 3 AND c.DATA_REVOGACAO IS NULL
                    ) THEN 1 ELSE 0 END,
                    DATA_CONSENTIMENTO = (
                        SELECT MAX(c.DATA_CONCESSAO) FROM TB_CONSENTIMENTO_LGPD c
                        WHERE c.RESPONSAVEL_ID = r.ID
                    )");

            migrationBuilder.DropTable(
                name: "TB_CONSENTIMENTO_LGPD");
        }
    }
}
