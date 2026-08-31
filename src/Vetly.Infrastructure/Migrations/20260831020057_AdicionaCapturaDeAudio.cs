using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCapturaDeAudio : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Captura de audio da consulta (RN-008/RN-009/RN-079): TB_SESSAO_CAPTURA,
        /// TB_SEGMENTO_AUDIO e TB_TRANSCRICAO, mais INICIADA_EM e ENCERRADA_EM em
        /// TB_CONSULTA. As duas colunas entram nullable: consulta antiga nunca passou
        /// por janela de captura.
        ///
        /// O audio e gravado em segmentos curtos, e nao num arquivo unico: assim a
        /// transcricao acontece durante o atendimento e a falha de um trecho nao derruba
        /// a consulta inteira.
        ///
        /// Dois indices unicos guardam invariantes que importam:
        /// - (SESSAO_CAPTURA_ID, SEQUENCIA): reenvio de segmento nao duplica texto;
        /// - SEGMENTO_AUDIO_ID em TB_TRANSCRICAO: callback reentregue nao duplica texto.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ENCERRADA_EM",
                table: "TB_CONSULTA",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "INICIADA_EM",
                table: "TB_CONSULTA",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TB_SEGMENTO_AUDIO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    SESSAO_CAPTURA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    SEQUENCIA = table.Column<int>(type: "NUMBER(6)", nullable: false),
                    MIDIA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    DURACAO_MS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    INICIO_RELATIVO_MS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ESTADO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FALHA_MOTIVO = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    TENTATIVAS = table.Column<byte>(type: "NUMBER(3)", nullable: false),
                    CALLBACK_TOKEN_HASH = table.Column<string>(type: "VARCHAR2(64)", maxLength: 64, nullable: true),
                    CRIADO_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_SEGMENTO_AUDIO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_SESSAO_CAPTURA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ESTADO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    INICIADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ENCERRADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CAPTURA_ATIVA = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_SESSAO_CAPTURA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_TRANSCRICAO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    SEGMENTO_AUDIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TEXTO = table.Column<string>(type: "CLOB", nullable: false),
                    CONFIANCA = table.Column<decimal>(type: "NUMBER(4,3)", nullable: true),
                    TRECHOS = table.Column<string>(type: "CLOB", nullable: true),
                    MOTOR = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: true),
                    CRIADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_TRANSCRICAO", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SEGMENTO_ESTADO",
                table: "TB_SEGMENTO_AUDIO",
                column: "ESTADO");

            migrationBuilder.CreateIndex(
                name: "IX_SEGMENTO_SESSAO_SEQUENCIA",
                table: "TB_SEGMENTO_AUDIO",
                columns: new[] { "SESSAO_CAPTURA_ID", "SEQUENCIA" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SESSAO_CAPTURA_CONSULTA",
                table: "TB_SESSAO_CAPTURA",
                column: "CONSULTA_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TRANSCRICAO_SEGMENTO",
                table: "TB_TRANSCRICAO",
                column: "SEGMENTO_AUDIO_ID",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_SEGMENTO_AUDIO");

            migrationBuilder.DropTable(
                name: "TB_SESSAO_CAPTURA");

            migrationBuilder.DropTable(
                name: "TB_TRANSCRICAO");

            migrationBuilder.DropColumn(
                name: "ENCERRADA_EM",
                table: "TB_CONSULTA");

            migrationBuilder.DropColumn(
                name: "INICIADA_EM",
                table: "TB_CONSULTA");
        }
    }
}
