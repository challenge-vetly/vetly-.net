using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vetly.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaTabelaDeCepCoordenada : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Tabela de apoio da geocodificacao simulada (RN-026, §5.6), com seed.
        ///
        /// Existe porque a RN-026 exige coordenada DERIVADA do endereco persistido, e nao
        /// dado mockado no front. O seed cobre CEPs de algumas capitais para que o
        /// matching funcione em desenvolvimento; CEP fora da base cai no centro da cidade,
        /// com a coordenada marcada para revisao.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TB_CEP_COORDENADA",
                columns: table => new
                {
                    CEP = table.Column<string>(type: "CHAR(8)", maxLength: 8, nullable: false),
                    LATITUDE = table.Column<decimal>(type: "NUMBER(9,6)", nullable: false),
                    LONGITUDE = table.Column<decimal>(type: "NUMBER(9,6)", nullable: false),
                    CIDADE = table.Column<string>(type: "VARCHAR2(150)", maxLength: 150, nullable: false),
                    UF = table.Column<string>(type: "CHAR(2)", maxLength: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TB_CEP_COORDENADA", x => x.CEP);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CEP_COORDENADA_CIDADE",
                table: "TB_CEP_COORDENADA",
                columns: new[] { "CIDADE", "UF" });

            // Seed da geocodificacao simulada (§5.6). Coordenadas APROXIMADAS, escolhidas
            // para dar realismo ao matching em desenvolvimento e demonstracao — nao sao
            // uma base de geocodificacao. Com o fornecedor real (pendencia P-02) esta
            // tabela deixa de existir.
            migrationBuilder.InsertData(
                table: "TB_CEP_COORDENADA",
                columns: new[] { "CEP", "LATITUDE", "LONGITUDE", "CIDADE", "UF" },
                values: new object[,]
                {
                    { "01310100", -23.561414m, -46.655881m, "Sao Paulo", "SP" },
                    { "01310200", -23.563000m, -46.654000m, "Sao Paulo", "SP" },
                    { "04538133", -23.585000m, -46.685000m, "Sao Paulo", "SP" },
                    { "05407002", -23.559000m, -46.686000m, "Sao Paulo", "SP" },
                    { "02011000", -23.503000m, -46.625000m, "Sao Paulo", "SP" },
                    { "03086000", -23.540000m, -46.575000m, "Sao Paulo", "SP" },
                    { "09040000", -23.663000m, -46.532000m, "Santo Andre", "SP" },
                    { "13010000", -22.905000m, -47.060000m, "Campinas", "SP" },
                    { "22071900", -22.971000m, -43.184000m, "Rio de Janeiro", "RJ" },
                    { "22410003", -22.984000m, -43.204000m, "Rio de Janeiro", "RJ" },
                    { "20040020", -22.906000m, -43.176000m, "Rio de Janeiro", "RJ" },
                    { "30130100", -19.921000m, -43.937000m, "Belo Horizonte", "MG" },
                    { "30112000", -19.937000m, -43.933000m, "Belo Horizonte", "MG" },
                    { "80010000", -25.430000m, -49.271000m, "Curitiba", "PR" },
                    { "90010150", -30.031000m, -51.229000m, "Porto Alegre", "RS" },
                    { "70040010", -15.795000m, -47.891000m, "Brasilia", "DF" },
                    { "40010000", -12.974000m, -38.512000m, "Salvador", "BA" },
                    { "50030230", -8.058000m, -34.883000m, "Recife", "PE" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TB_CEP_COORDENADA");
        }
    }
}
