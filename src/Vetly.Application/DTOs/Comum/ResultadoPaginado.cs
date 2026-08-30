namespace Vetly.Application.DTOs.Comum;

/// <summary>
/// Envelope padrão das listagens paginadas da API (§2.3 do documento de engenharia).
/// Listas grandes deixam de devolver tudo de uma vez, que é insustentável no volume
/// real da agenda e do financeiro.
/// </summary>
/// <typeparam name="T">Tipo do item da página.</typeparam>
public class ResultadoPaginado<T>
{
    /// <summary>Itens da página solicitada.</summary>
    public IReadOnlyList<T> Itens { get; init; } = [];

    /// <summary>Total de registros que atendem ao filtro, ignorando a paginação.</summary>
    public int Total { get; init; }

    /// <summary>Página retornada, começando em 1.</summary>
    public int Pagina { get; init; }

    /// <summary>Quantidade de itens por página efetivamente aplicada.</summary>
    public int Tamanho { get; init; }

    /// <summary>Quantidade total de páginas para o filtro atual.</summary>
    public int TotalDePaginas => Tamanho == 0 ? 0 : (int)Math.Ceiling(Total / (double)Tamanho);

    /// <summary>Indica se ainda há página seguinte.</summary>
    public bool TemProximaPagina => Pagina < TotalDePaginas;

    public ResultadoPaginado() { }

    public ResultadoPaginado(IReadOnlyList<T> itens, int total, Paginacao paginacao)
    {
        Itens = itens;
        Total = total;
        Pagina = paginacao.Pagina;
        Tamanho = paginacao.Tamanho;
    }

    /// <summary>Projeta os itens da página preservando os metadados de paginação.</summary>
    public ResultadoPaginado<TDestino> Mapear<TDestino>(Func<T, TDestino> projecao) => new()
    {
        Itens = [.. Itens.Select(projecao)],
        Total = Total,
        Pagina = Pagina,
        Tamanho = Tamanho
    };
}
