using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Comum;

/// <summary>
/// Parâmetros de paginação das listagens (<c>?pagina=&amp;tamanho=</c>).
/// Valores fora da faixa são normalizados em vez de rejeitados: paginação é
/// conveniência de leitura, não regra de negócio.
/// </summary>
public class Paginacao
{
    /// <summary>Tamanho de página adotado quando o cliente não informa nada.</summary>
    public const int TamanhoPadrao = 20;

    /// <summary>
    /// Teto de itens por página. Existe para que um cliente não consiga pedir a
    /// tabela inteira em uma requisição e derrubar o tempo de resposta.
    /// </summary>
    public const int TamanhoMaximo = 100;

    private readonly int _pagina = 1;
    private readonly int _tamanho = TamanhoPadrao;

    /// <summary>Página desejada, começando em 1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a 1.")]
    public int Pagina
    {
        get => _pagina;
        init => _pagina = value < 1 ? 1 : value;
    }

    /// <summary>Itens por página. Limitado a <see cref="TamanhoMaximo"/>.</summary>
    [Range(1, TamanhoMaximo, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
    public int Tamanho
    {
        get => _tamanho;
        init => _tamanho = value switch
        {
            < 1 => TamanhoPadrao,
            > TamanhoMaximo => TamanhoMaximo,
            _ => value
        };
    }

    /// <summary>Quantidade de registros a pular na consulta.</summary>
    public int Deslocamento => (Pagina - 1) * Tamanho;
}
