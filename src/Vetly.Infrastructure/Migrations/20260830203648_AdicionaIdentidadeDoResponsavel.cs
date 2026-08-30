using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIdentidadeDoResponsavel : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Fundacao da identidade do Responsavel (onda 2).
        ///
        /// TB_TUTOR (aditivo): SENHA_HASH para o login pelo app, as duas finalidades de
        /// consentimento que faltavam (promocoes — RN-093 — e dados agregados — RN-075/077)
        /// e as datas de concessao/revogacao POR FINALIDADE, que a LGPD exige (RN-061/062)
        /// e que um unico DATA_CONSENTIMENTO nao expressa.
        ///
        /// TB_REFRESH_TOKEN e TB_DISPOSITIVO sao tabelas novas. O refresh token guarda so o
        /// HASH do valor entregue ao cliente: vazamento da tabela nao permite se passar pelo
        /// usuario.
        ///
        /// Backfill: para os tutores que ja tinham consentimento registrado, a data de
        /// concessao das finalidades concedidas recebe o DATA_CONSENTIMENTO existente — e a
        /// unica data que a plataforma tem, e deixa-la nula faria o app exibir "concedido"
        /// sem quando, que e exatamente o que a RN-061 quer evitar.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CONSENTIMENTO_DADOS_AGREGADOS",
                table: "TB_TUTOR",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CONSENTIMENTO_PROMOCOES",
                table: "TB_TUTOR",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_CONCESSAO_ATENDIMENTO",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_CONCESSAO_COMPARTILHAMENTO",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_CONCESSAO_DADOS_AGREGADOS",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_CONCESSAO_LEMBRETES",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_CONCESSAO_PROMOCOES",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_REVOGACAO_ATENDIMENTO",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_REVOGACAO_COMPARTILHAMENTO",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_REVOGACAO_DADOS_AGREGADOS",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_REVOGACAO_LEMBRETES",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DATA_REVOGACAO_PROMOCOES",
                table: "TB_TUTOR",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SENHA_HASH",
                table: "TB_TUTOR",
                type: "VARCHAR2(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TB_DISPOSITIVO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    PUSH_TOKEN = table.Column<string>(type: "VARCHAR2(255)", maxLength: 255, nullable: false),
                    PLATAFORMA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    REGISTRADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ULTIMO_USO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_DISPOSITIVO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_REFRESH_TOKEN",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    USUARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO_USUARIO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HASH = table.Column<string>(type: "VARCHAR2(64)", maxLength: 64, nullable: false),
                    CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    EXPIRA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    REVOGADO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    REVOGADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    SUBSTITUIDO_POR_ID = table.Column<string>(type: "CHAR(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_REFRESH_TOKEN", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DISPOSITIVO_PUSH_TOKEN",
                table: "TB_DISPOSITIVO",
                column: "PUSH_TOKEN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DISPOSITIVO_TUTOR",
                table: "TB_DISPOSITIVO",
                columns: new[] { "TUTOR_ID", "ATIVO" });

            migrationBuilder.CreateIndex(
                name: "IX_REFRESH_TOKEN_HASH",
                table: "TB_REFRESH_TOKEN",
                column: "HASH",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_REFRESH_TOKEN_USUARIO",
                table: "TB_REFRESH_TOKEN",
                columns: new[] { "USUARIO_ID", "REVOGADO" });
            // Backfill das datas de concessao a partir do DATA_CONSENTIMENTO existente,
            // apenas para as finalidades que estavam concedidas.
            migrationBuilder.Sql(@"
                UPDATE TB_TUTOR
                   SET DATA_CONCESSAO_ATENDIMENTO      = CASE WHEN CONSENTIMENTO_ATENDIMENTO = 1      THEN DATA_CONSENTIMENTO END,
                       DATA_CONCESSAO_LEMBRETES        = CASE WHEN CONSENTIMENTO_LEMBRETES = 1        THEN DATA_CONSENTIMENTO END,
                       DATA_CONCESSAO_COMPARTILHAMENTO = CASE WHEN CONSENTIMENTO_COMPARTILHAMENTO = 1 THEN DATA_CONSENTIMENTO END
                 WHERE DATA_CONSENTIMENTO IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_DISPOSITIVO");

            migrationBuilder.DropTable(
                name: "TB_REFRESH_TOKEN");

            migrationBuilder.DropColumn(
                name: "CONSENTIMENTO_DADOS_AGREGADOS",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "CONSENTIMENTO_PROMOCOES",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_CONCESSAO_ATENDIMENTO",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_CONCESSAO_COMPARTILHAMENTO",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_CONCESSAO_DADOS_AGREGADOS",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_CONCESSAO_LEMBRETES",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_CONCESSAO_PROMOCOES",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_REVOGACAO_ATENDIMENTO",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_REVOGACAO_COMPARTILHAMENTO",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_REVOGACAO_DADOS_AGREGADOS",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_REVOGACAO_LEMBRETES",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "DATA_REVOGACAO_PROMOCOES",
                table: "TB_TUTOR");

            migrationBuilder.DropColumn(
                name: "SENHA_HASH",
                table: "TB_TUTOR");
        }
    }
}
