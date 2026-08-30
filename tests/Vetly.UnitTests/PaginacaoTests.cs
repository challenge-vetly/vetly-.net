using Vetly.Application.DTOs.Comum;

namespace Vetly.UnitTests;

/// <summary>
/// Testes do envelope de paginacao das listagens (§2.3 do documento de engenharia).
/// Paginacao e conveniencia de leitura: valor fora da faixa e normalizado, nao rejeitado.
/// </summary>
public class PaginacaoTests
{
    [Fact]
    public void Paginacao_SemParametros_UsaPagina1ETamanho20()
    {
        var paginacao = new Paginacao();

        Assert.Equal(1, paginacao.Pagina);
        Assert.Equal(20, paginacao.Tamanho);
        Assert.Equal(0, paginacao.Deslocamento);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Paginacao_NormalizaPaginaInvalidaParaAPrimeira(int informado, int esperado)
    {
        var paginacao = new Paginacao { Pagina = informado };

        Assert.Equal(esperado, paginacao.Pagina);
    }

    [Theory]
    [InlineData(0, 20)]      // sem tamanho valido, vale o padrao
    [InlineData(50, 50)]
    [InlineData(500, 100)]   // teto: ninguem pede a tabela inteira numa requisicao
    public void Paginacao_LimitaOTamanhoDaPagina(int informado, int esperado)
    {
        var paginacao = new Paginacao { Tamanho = informado };

        Assert.Equal(esperado, paginacao.Tamanho);
    }

    [Fact]
    public void Deslocamento_PulaAsPaginasAnteriores()
    {
        var paginacao = new Paginacao { Pagina = 4, Tamanho = 25 };

        Assert.Equal(75, paginacao.Deslocamento);
    }

    [Fact]
    public void ResultadoPaginado_CalculaTotalDePaginasEProximaPagina()
    {
        var resultado = new ResultadoPaginado<string>(
            ["a", "b"], total: 45, new Paginacao { Pagina = 1, Tamanho = 20 });

        Assert.Equal(3, resultado.TotalDePaginas);   // 45 itens em paginas de 20
        Assert.True(resultado.TemProximaPagina);
    }

    [Fact]
    public void ResultadoPaginado_UltimaPagina_NaoTemProxima()
    {
        var resultado = new ResultadoPaginado<string>(
            ["a"], total: 41, new Paginacao { Pagina = 3, Tamanho = 20 });

        Assert.Equal(3, resultado.TotalDePaginas);
        Assert.False(resultado.TemProximaPagina);
    }

    [Fact]
    public void ResultadoPaginado_SemItens_NaoTemPaginas()
    {
        var resultado = new ResultadoPaginado<string>([], total: 0, new Paginacao());

        Assert.Equal(0, resultado.TotalDePaginas);
        Assert.False(resultado.TemProximaPagina);
        Assert.Empty(resultado.Itens);
    }

    [Fact]
    public void Mapear_ProjetaOsItensPreservandoOsMetadados()
    {
        var origem = new ResultadoPaginado<int>(
            [1, 2, 3], total: 30, new Paginacao { Pagina = 2, Tamanho = 3 });

        var destino = origem.Mapear(i => i.ToString());

        Assert.Equal(["1", "2", "3"], destino.Itens);
        Assert.Equal(30, destino.Total);
        Assert.Equal(2, destino.Pagina);
        Assert.Equal(3, destino.Tamanho);
    }
}
