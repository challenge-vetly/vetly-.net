using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// TB_OBRIGACAO_PET: obrigacoes recorrentes de cuidado do animal (RN-045/RN-046).
    ///
    /// Aditiva — tabela nova, nenhuma coluna existente e tocada.
    ///
    /// PERIODICIDADE_DIAS guarda o intervalo, e nao uma data solta: cumprir empurra o
    /// proximo vencimento sozinho. Zero significa obrigacao de uma vez so, que se
    /// arquiva ao ser cumprida em vez de ficar eternamente vencida no board.
    ///
    /// ARQUIVADA e soft delete: o animal muda de protocolo, mas o historico do que ja
    /// foi cumprido continua valendo.
    ///
    /// Sem indice unico: o mesmo animal pode ter duas obrigacoes do mesmo tipo com
    /// descricoes diferentes (V10 e antirrabica sao ambas vacina).
    /// </summary>
    public partial class AdicionaObrigacoesDoPet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_OBRIGACAO_PET",
                columns: table => new
                {
                    ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    ANIMAL_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TUTOR_ID = table.Column<string>(type: "CHAR(36)", nullable: false),
                    TIPO = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    DESCRICAO = table.Column<string>(type: "VARCHAR2(120)", maxLength: 120, nullable: false),
                    PERIODICIDADE_DIAS = table.Column<short>(type: "NUMBER(5)", nullable: false),
                    PROXIMO_VENCIMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    ULTIMO_CUMPRIMENTO = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: true),
                    ULTIMA_CONSULTA_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    REGISTRADA_POR_VET_ID = table.Column<string>(type: "CHAR(36)", nullable: true),
                    DERIVADA_CARTEIRA = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    ARQUIVADA = table.Column<bool>(type: "NUMBER(1)", nullable: false),
                    CRIADA_EM = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_OBRIGACAO_PET", x => x.ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OBRIGACAO_ANIMAL",
                table: "TB_OBRIGACAO_PET",
                columns: new[] { "ANIMAL_ID", "ARQUIVADA" });

            migrationBuilder.CreateIndex(
                name: "IX_OBRIGACAO_VENCIMENTO",
                table: "TB_OBRIGACAO_PET",
                column: "PROXIMO_VENCIMENTO");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_OBRIGACAO_PET");
        }
    }
}
