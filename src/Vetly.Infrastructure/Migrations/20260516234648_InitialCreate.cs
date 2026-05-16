using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_ANIMAL",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    NOME = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: false),
                    ESPECIE = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: false),
                    RACA = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: false),
                    DATA_NASCIMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ALERTAS_ATIVOS = table.Column<string>(type: "VARCHAR2(2000)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_ANIMAL", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_CONSULTA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    DATA_HORA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    MODALIDADE = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    DIAGNOSTICO_VALIDADO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    PROTOCOLO_VALIDADO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    STATUS_PAGAMENTO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    CANCELADA = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    FINALIZADA = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_CONSULTA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_DOCUMENTO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    INTERNACAO_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    TIPO_DOCUMENTO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    VERSAO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA_GERACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    CRMV_SIGNATARIO = table.Column<string>(type: "VARCHAR2(15)", maxLength: 15, nullable: false),
                    ASSINADO_DIGITALMENTE = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    VERSAO_ORIGINAL_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    DATA_CORRECAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    CRMV_SOLICITANTE_CORRECAO = table.Column<string>(type: "VARCHAR2(15)", maxLength: 15, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_DOCUMENTO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_EMPRESA",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    NOME = table.Column<string>(type: "VARCHAR2(300)", maxLength: 300, nullable: false),
                    TIPO = table.Column<string>(type: "VARCHAR2(100)", maxLength: 100, nullable: false),
                    ADMINISTRADOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ATIVA = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_EMPRESA", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_EXAME",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO_SOLICITACAO = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: false),
                    RESULTADO = table.Column<string>(type: "CLOB", nullable: true),
                    LIBERADO_AO_TUTOR = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DATA_SOLICITACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DATA_RESULTADO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_EXAME", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_INTERNACAO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VETERINARIO_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    VALOR_CAUCAO = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    VALOR_TOTAL_APURADO = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    DATA_ABERTURA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    DATA_ALTA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    PROCEDIMENTOS_DIARIOS = table.Column<string>(type: "CLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_INTERNACAO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_LEMBRETE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DATA_EVENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    TENTATIVAS_REALIZADAS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TUTOR_RESPONDEU = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ALERTA_ENVIADO_CLINICA = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_LEMBRETE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_PAGAMENTO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    INTERNACAO_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    VALOR = table.Column<decimal>(type: "NUMBER(18,2)", nullable: false),
                    MEIO_PAGAMENTO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    MOMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    STATUS_PAGAMENTO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PERCENTUAL_SPLIT = table.Column<decimal>(type: "NUMBER(5,2)", nullable: false),
                    VALOR_ESTORNADO = table.Column<decimal>(type: "NUMBER(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_PAGAMENTO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_PRONTUARIO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    DADOS_CLINICOS = table.Column<string>(type: "CLOB", nullable: false),
                    VERSAO_ORIGINAL_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    DATA_CORRECAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    JUSTIFICATIVA_CORRECAO = table.Column<string>(type: "VARCHAR2(1000)", maxLength: 1000, nullable: true),
                    CRMV_SOLICITANTE_CORRECAO = table.Column<string>(type: "VARCHAR2(15)", maxLength: 15, nullable: true),
                    DATA_CRIACAO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_PRONTUARIO", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_TUTOR",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    NOME = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: false),
                    EMAIL = table.Column<string>(type: "VARCHAR2(254)", maxLength: 254, nullable: false),
                    TELEFONE = table.Column<string>(type: "VARCHAR2(20)", maxLength: 20, nullable: false),
                    CONSENTIMENTO_ATENDIMENTO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CONSENTIMENTO_LEMBRETES = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CONSENTIMENTO_COMPARTILHAMENTO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    DATA_CONSENTIMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_TUTOR", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "TB_VETERINARIO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    NOME = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: false),
                    CRMV = table.Column<string>(type: "VARCHAR2(15)", nullable: false),
                    UF_ATUACAO = table.Column<string>(type: "CHAR(2)", maxLength: 2, nullable: false),
                    ESPECIALIDADES = table.Column<string>(type: "VARCHAR2(1000)", nullable: false),
                    ESPECIES_ATENDIDAS = table.Column<string>(type: "VARCHAR2(1000)", nullable: false),
                    TITULACAO_ACADEMICA = table.Column<string>(type: "VARCHAR2(300)", maxLength: 300, nullable: true),
                    PERSONA = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PLANO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ATIVO = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    EMPRESA_ID = table.Column<string>(type: "CHAR(36)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_VETERINARIO", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ANIMAL_TUTOR",
                table: "TB_ANIMAL",
                column: "TUTOR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CONSULTA_ANIMAL",
                table: "TB_CONSULTA",
                column: "ANIMAL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CONSULTA_DATA",
                table: "TB_CONSULTA",
                column: "DATA_HORA");

            migrationBuilder.CreateIndex(
                name: "IX_CONSULTA_VETERINARIO",
                table: "TB_CONSULTA",
                column: "VETERINARIO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENTO_CONSULTA",
                table: "TB_DOCUMENTO",
                column: "CONSULTA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_DOCUMENTO_INTERNACAO",
                table: "TB_DOCUMENTO",
                column: "INTERNACAO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EMPRESA_ADMINISTRADOR",
                table: "TB_EMPRESA",
                column: "ADMINISTRADOR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXAME_ANIMAL",
                table: "TB_EXAME",
                column: "ANIMAL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_EXAME_VETERINARIO",
                table: "TB_EXAME",
                column: "VETERINARIO_ID");

            migrationBuilder.CreateIndex(
                name: "IX_INTERNACAO_ANIMAL",
                table: "TB_INTERNACAO",
                column: "ANIMAL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LEMBRETE_ANIMAL",
                table: "TB_LEMBRETE",
                column: "ANIMAL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_LEMBRETE_TUTOR",
                table: "TB_LEMBRETE",
                column: "TUTOR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAGAMENTO_CONSULTA",
                table: "TB_PAGAMENTO",
                column: "CONSULTA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PAGAMENTO_TUTOR",
                table: "TB_PAGAMENTO",
                column: "TUTOR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRONTUARIO_ANIMAL",
                table: "TB_PRONTUARIO",
                column: "ANIMAL_ID");

            migrationBuilder.CreateIndex(
                name: "IX_PRONTUARIO_CONSULTA",
                table: "TB_PRONTUARIO",
                column: "CONSULTA_ID");

            migrationBuilder.CreateIndex(
                name: "IX_TUTOR_EMAIL",
                table: "TB_TUTOR",
                column: "EMAIL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VETERINARIO_UF",
                table: "TB_VETERINARIO",
                column: "UF_ATUACAO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_ANIMAL");

            migrationBuilder.DropTable(
                name: "TB_CONSULTA");

            migrationBuilder.DropTable(
                name: "TB_DOCUMENTO");

            migrationBuilder.DropTable(
                name: "TB_EMPRESA");

            migrationBuilder.DropTable(
                name: "TB_EXAME");

            migrationBuilder.DropTable(
                name: "TB_INTERNACAO");

            migrationBuilder.DropTable(
                name: "TB_LEMBRETE");

            migrationBuilder.DropTable(
                name: "TB_PAGAMENTO");

            migrationBuilder.DropTable(
                name: "TB_PRONTUARIO");

            migrationBuilder.DropTable(
                name: "TB_TUTOR");

            migrationBuilder.DropTable(
                name: "TB_VETERINARIO");
        }
    }
}
