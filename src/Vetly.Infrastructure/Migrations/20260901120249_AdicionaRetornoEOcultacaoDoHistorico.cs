using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <summary>
    /// Duas colunas aditivas: o vinculo do retorno e a ocultacao do historico.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>TB_CONSULTA.CONSULTA_ORIGEM_ID</b> (RN-013): aponta para a consulta que
    /// originou um retorno. Nula em tudo que nao e retorno — que e o estado correto de
    /// todo o historico existente, e por isso nao ha backfill. Sem indice de proposito:
    /// a coluna e quase toda nula, e um indice ai custaria escrita sem pagar leitura.
    /// </para>
    /// <para>
    /// <b>TB_PRONTUARIO.OCULTO</b> (RN-068): esconde o registro do board do Responsavel
    /// sem apagar nada — o veterinario continua vendo, e a guarda regulatoria do
    /// prontuario permanece. O padrao <c>0</c> nao e escolha de conveniencia: registro
    /// que ninguem escondeu tem de continuar visivel, e um padrao <c>1</c> faria todo o
    /// historico clinico ja existente sumir do app de uma vez.
    /// </para>
    /// </remarks>
    public partial class AdicionaRetornoEOcultacaoDoHistorico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OCULTO",
                table: "TB_PRONTUARIO",
                type: "NUMBER(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "CONSULTA_ORIGEM_ID",
                table: "TB_CONSULTA",
                type: "CHAR(36)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OCULTO",
                table: "TB_PRONTUARIO");

            migrationBuilder.DropColumn(
                name: "CONSULTA_ORIGEM_ID",
                table: "TB_CONSULTA");
        }
    }
}
