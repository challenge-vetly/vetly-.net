using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// TB_NOTIFICACAO: caixa de entrada do Responsavel (RN-092/RN-093).
    ///
    /// Aditiva — tabela nova, nenhuma coluna existente e tocada.
    ///
    /// A notificacao e gravada ANTES de ser enviada, e nao gerada no momento do
    /// disparo. Duas razoes: o app precisa de uma caixa que sobrevive ao push perdido
    /// — aparelho desligado, token trocado, permissao negada — e o historico do que
    /// foi comunicado e o que permite responder "avisamos?" depois.
    ///
    /// STATUS = NaoEntregue nao e o fim da linha: a linha continua na caixa e o
    /// Responsavel a ve ao abrir o app. Push perdido nao pode significar aviso
    /// perdido.
    ///
    /// IX_NOTIFICACAO_STATUS_AGENDADA e o indice da rotina de envio; o par
    /// ANIMAL_ID/TIPO serve a regua de lembretes, que pergunta "ja avisei sobre este
    /// animal na ultima semana?" antes de avisar de novo.
    /// </summary>
    public partial class AdicionaNotificacoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_NOTIFICACAO",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TITULO = table.Column<string>(type: "VARCHAR2(120)", maxLength: 120, nullable: false),
                    CORPO = table.Column<string>(type: "VARCHAR2(500)", maxLength: 500, nullable: false),
                    STATUS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    DESTINO = table.Column<string>(type: "VARCHAR2(200)", maxLength: 200, nullable: true),
                    AGENDADA_PARA = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ENVIADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    LIDA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    TENTATIVAS = table.Column<byte>(type: "NUMBER(3)", nullable: false),
                    ULTIMO_ERRO = table.Column<string>(type: "VARCHAR2(300)", maxLength: 300, nullable: true),
                    CRIADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_NOTIFICACAO", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICACAO_ANIMAL_TIPO",
                table: "TB_NOTIFICACAO",
                columns: new[] { "ANIMAL_ID", "TIPO" });

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICACAO_STATUS_AGENDADA",
                table: "TB_NOTIFICACAO",
                columns: new[] { "STATUS", "AGENDADA_PARA" });

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICACAO_TUTOR_CRIADA",
                table: "TB_NOTIFICACAO",
                columns: new[] { "TUTOR_ID", "CRIADA_EM" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_NOTIFICACAO");
        }
    }
}
