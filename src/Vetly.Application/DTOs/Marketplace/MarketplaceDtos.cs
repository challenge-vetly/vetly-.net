using System.ComponentModel.DataAnnotations;
using Vetly.Application.DTOs.Comum;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Marketplace;

/// <summary>Item do catálogo do marketplace (RN-108).</summary>
public class ItemMarketplaceDto
{
    /// <summary>Identificador do item — é o que o resgate envia.</summary>
    public Guid Id { get; set; }

    /// <summary>Nome exibido no catálogo.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Descrição do item.</summary>
    public string? Descricao { get; set; }

    /// <summary>Categoria (RN-099).</summary>
    public CategoriaItem Categoria { get; set; }

    /// <summary>Quem fornece o item.</summary>
    public string Fornecedor { get; set; } = string.Empty;

    /// <summary>Pontos debitados no resgate. Decidido pelo servidor (RN-108).</summary>
    public int CustoEmPontos { get; set; }

    /// <summary>
    /// Quanto os pontos do item valem em desconto — 100 pontos = R$ 3,00 (RN-049).
    /// É o valor que o cupom carrega.
    /// </summary>
    public decimal DescontoEmReais { get; set; }

    /// <summary>Preço de prateleira, informativo.</summary>
    public decimal ValorDeReferencia { get; set; }

    /// <summary>Imagem do item, quando houver.</summary>
    public Guid? ImagemMidiaId { get; set; }

    /// <summary>Unidades disponíveis; nulo é ilimitado (RN-109).</summary>
    public int? Estoque { get; set; }

    /// <summary>Ativo e com estoque: dá para resgatar agora.</summary>
    public bool Disponivel { get; set; }

    /// <summary>Visível no catálogo do Responsável.</summary>
    public bool Ativo { get; set; }

    /// <summary>Momento do cadastro (UTC).</summary>
    public DateTime CriadoEm { get; set; }

    /// <summary>Última alteração (UTC).</summary>
    public DateTime? AtualizadoEm { get; set; }
}

/// <summary>
/// Detalhe do item com a situação do Responsável: se o saldo cobre e quanto o cupom
/// vale, calculado pelas mesmas regras do resgate (RN-049/RN-051).
/// </summary>
public class ItemMarketplaceDetalheDto : ItemMarketplaceDto
{
    /// <summary>
    /// Saldo do Responsável. Nulo para quem não é Responsável — veterinário e Admin
    /// não têm pontos, e um zero ali pareceria saldo vazio.
    /// </summary>
    public int? SaldoAtual { get; set; }

    /// <summary>O saldo cobre o custo do item. Nulo fora do perfil Responsável.</summary>
    public bool? SaldoSuficiente { get; set; }

    /// <summary>Quanto falta para resgatar; zero quando o saldo cobre.</summary>
    public int? PontosQueFaltam { get; set; }

    /// <summary>Faixa de financiamento do desconto (RN-051).</summary>
    public FaixaDeFinanciamento Faixa { get; set; }

    /// <summary>Percentual do desconto absorvido pela Vetly (RN-051).</summary>
    public decimal PercentualVetly { get; set; }

    /// <summary>Percentual do desconto absorvido pelo prestador (RN-051).</summary>
    public decimal PercentualPrestador { get; set; }

    /// <summary>Validade do cupom, em dias (RN-053).</summary>
    public int ValidadeDoCupomEmDias { get; set; }

    /// <summary>
    /// Sempre <c>Simulado</c> no MVP: a divisão é calculada, gravada e exibida, sem
    /// abatimento financeiro real (RN-051).
    /// </summary>
    public string Abatimento { get; set; } = "Simulado";
}

/// <summary>
/// Filtros do catálogo (<c>GET /api/marketplace/itens</c>). Ordenação por
/// <c>ordenarPor=nome|custoEmPontos|criadoEm</c>.
/// </summary>
public class FiltroItemMarketplaceDto
{
    /// <summary>Só itens desta categoria.</summary>
    public CategoriaItem? Categoria { get; set; }

    /// <summary>Trecho do nome, sem distinção de caixa.</summary>
    [MaxLength(120)]
    public string? Texto { get; set; }

    /// <summary>Custo mínimo em pontos, inclusivo.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "pontosMin nao pode ser negativo.")]
    public int? PontosMin { get; set; }

    /// <summary>Custo máximo em pontos, inclusivo.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "pontosMax nao pode ser negativo.")]
    public int? PontosMax { get; set; }

    /// <summary>
    /// Só o que o saldo do Responsável cobre. Para quem não tem pontos (veterinário,
    /// Admin) é ignorado: não há saldo contra o qual filtrar.
    /// </summary>
    public bool ApenasAcessiveis { get; set; }

    /// <summary>Inclui itens desativados. Só vale para o Admin, que mantém o catálogo.</summary>
    public bool IncluirInativos { get; set; }
}

/// <summary>Cadastro de item no catálogo (Admin).</summary>
public class CriarItemMarketplaceDto
{
    /// <summary>Nome exibido no catálogo.</summary>
    [Required(ErrorMessage = "O nome e obrigatorio.")]
    [MaxLength(120, ErrorMessage = "O nome aceita ate 120 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>Descrição do item.</summary>
    [MaxLength(1000, ErrorMessage = "A descricao aceita ate 1000 caracteres.")]
    public string? Descricao { get; set; }

    /// <summary>Categoria (RN-099). Define a taxa de listagem (RN-111).</summary>
    [Required(ErrorMessage = "A categoria e obrigatoria.")]
    [EnumDataType(typeof(CategoriaItem), ErrorMessage = "Categoria desconhecida.")]
    public CategoriaItem? Categoria { get; set; }

    /// <summary>Quem fornece o item.</summary>
    [Required(ErrorMessage = "O fornecedor e obrigatorio.")]
    [MaxLength(120, ErrorMessage = "O fornecedor aceita ate 120 caracteres.")]
    public string Fornecedor { get; set; } = string.Empty;

    /// <summary>Pontos debitados no resgate.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "O custo em pontos deve ser positivo.")]
    public int CustoEmPontos { get; set; }

    /// <summary>Preço de prateleira, em reais.</summary>
    // Limites em cultura invariante: sem isso o "9999999999.99" e lido pela cultura
    // do servidor, e numa maquina pt-BR o ponto nao e separador decimal — o parse
    // falha dentro da validacao e a requisicao vira 500 em vez de 400.
    [Range(typeof(decimal), "0", "9999999999.99",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "O valor de referencia deve estar entre 0 e 9999999999,99.")]
    public decimal ValorDeReferencia { get; set; }

    /// <summary>Imagem já enviada pelo fluxo de mídia.</summary>
    public Guid? ImagemMidiaId { get; set; }

    /// <summary>Unidades disponíveis; nulo é ilimitado (RN-109).</summary>
    [Range(0, int.MaxValue, ErrorMessage = "O estoque nao pode ser negativo.")]
    public int? Estoque { get; set; }
}

/// <summary>Alteração de item do catálogo (Admin).</summary>
public class AtualizarItemMarketplaceDto : CriarItemMarketplaceDto
{
    /// <summary>
    /// <c>true</c> reativa um item desativado; nulo mantém como está. Desativar é o
    /// <c>DELETE</c>, que é o verbo que o app já usa para "tirar do catálogo".
    /// </summary>
    public bool? Ativo { get; set; }
}

/// <summary>Resgate de um item do catálogo (RN-108).</summary>
public class ResgatarItemDto
{
    /// <summary>
    /// Item do catálogo. É o único dado que o cliente envia: nome, categoria e custo
    /// vêm do catálogo, nunca do pedido.
    /// </summary>
    [Required(ErrorMessage = "Informe o item.")]
    [GuidObrigatorio(ErrorMessage = "Informe o item.")]
    public Guid ItemId { get; set; }
}

/// <summary>Validação do cupom no estabelecimento (RN-110).</summary>
public class ValidarCupomDto
{
    /// <summary>Código lido do QR ou digitado (ex.: <c>VETLY-ABCD2345EFGH</c>).</summary>
    [Required(ErrorMessage = "Informe o codigo do cupom.")]
    [MaxLength(40, ErrorMessage = "Codigo de cupom invalido.")]
    public string CodigoQr { get; set; } = string.Empty;
}

/// <summary>Taxa de listagem de uma categoria (RN-111).</summary>
public class TaxaDeListagemDto
{
    /// <summary>Categoria do catálogo.</summary>
    public CategoriaItem Categoria { get; set; }

    /// <summary>Valor da taxa, em reais.</summary>
    public decimal Valor { get; set; }

    /// <summary>Sempre falso no MVP: a taxa é registrada, não cobrada.</summary>
    public bool Cobrada { get; set; }

    /// <summary>Itens ativos da categoria hoje — a base sobre a qual a taxa incidiria.</summary>
    public int ItensAtivos { get; set; }
}
