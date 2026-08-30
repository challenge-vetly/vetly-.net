using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaConteudoEPublicacaoDocumento : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Migration ADITIVA em TB_DOCUMENTO. Ate aqui a tabela guardava so metadados: o
        /// conteudo do documento nao persistia, entao o documento nao existia de fato para
        /// o Responsavel no board do pet (RN-090).
        ///
        /// Todas as colunas entram nullable — nenhum backfill e possivel nem desejavel:
        /// os documentos ja gerados nao tem conteudo para recuperar, e inventar texto em
        /// documento clinico seria pior que deixar nulo.
        ///
        /// Quem escreve CONTEUDO e PDF_MIDIA_ID e a geracao a partir do estado final
        /// aprovado pelo veterinario (RN-083), que entra nas ondas 5 e 6 junto do
        /// IAssinaturaAdapter (ASSINATURA_METODO/CARIMBO) e da publicacao no board
        /// (PUBLICADO_EM/LIDO_EM). O schema entra agora para nao exigir uma segunda
        /// migration na tabela depois.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ASSINATURA_CARIMBO",
                table: "TB_DOCUMENTO",
                type: "VARCHAR2(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ASSINATURA_METODO",
                table: "TB_DOCUMENTO",
                type: "VARCHAR2(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CONTEUDO",
                table: "TB_DOCUMENTO",
                type: "CLOB",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LIDO_EM",
                table: "TB_DOCUMENTO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PDF_MIDIA_ID",
                table: "TB_DOCUMENTO",
                type: "CHAR(36)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PUBLICADO_EM",
                table: "TB_DOCUMENTO",
                type: "TIMESTAMP(7)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SUBTIPO",
                table: "TB_DOCUMENTO",
                type: "NUMBER(10)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ASSINATURA_CARIMBO",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "ASSINATURA_METODO",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "CONTEUDO",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "LIDO_EM",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "PDF_MIDIA_ID",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "PUBLICADO_EM",
                table: "TB_DOCUMENTO");

            migrationBuilder.DropColumn(
                name: "SUBTIPO",
                table: "TB_DOCUMENTO");
        }
    }
}
