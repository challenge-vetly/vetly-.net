using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Item do catálogo do marketplace, trocável por pontos (RN-108 a RN-111).
///
/// <para>
/// <b>Por que o catálogo saiu do front.</b> Até a Sprint 4 o app mantinha uma lista
/// fixa e mandava no resgate o item, a categoria e <i>quantos pontos debitar</i>
/// (RN-098). Era o cliente decidindo o preço: um app adulterado trocava 10 pontos por
/// qualquer coisa. Com o catálogo aqui, o resgate leva só o id do item e o servidor
/// deriva nome, categoria e custo — o mesmo princípio de "o servidor decide preço,
/// pontos e desconto" que já vale para consulta e split.
/// </para>
/// <para>
/// O item <b>não é apagado</b>: desativar preserva o histórico dos cupons que
/// apontam para ele e permite reativar sem recadastrar.
/// </para>
/// </summary>
public class ItemMarketplace
{
    /// <summary>Identificador do item (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Nome exibido no catálogo e copiado para o cupom.</summary>
    [Required]
    [MaxLength(120)]
    public string Nome { get; private set; }

    /// <summary>Descrição do item, como aparece no detalhe do app.</summary>
    [MaxLength(1000)]
    public string? Descricao { get; private set; }

    /// <summary>Categoria na taxonomia que sustenta a taxa de listagem (RN-099/RN-111).</summary>
    [Required]
    public CategoriaItem Categoria { get; private set; }

    /// <summary>
    /// Quem fornece o item. Texto, e não chave para um cadastro de fornecedores: no
    /// MVP não há portal do parceiro, e um cadastro sem dono seria tabela vazia.
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string Fornecedor { get; private set; }

    /// <summary>Pontos debitados no resgate. Sempre positivo (RN-108).</summary>
    [Range(1, int.MaxValue)]
    public int CustoEmPontos { get; private set; }

    /// <summary>
    /// Preço de prateleira do item, em reais. Informativo: mostra ao Responsável o
    /// tamanho do benefício. O desconto do cupom continua sendo o valor dos pontos
    /// (RN-049), não este preço.
    /// </summary>
    [Range(0, 9999999999.99)]
    public decimal ValorDeReferencia { get; private set; }

    /// <summary>Imagem do item, enviada pelo fluxo de mídia. Opcional.</summary>
    public Guid? ImagemMidiaId { get; private set; }

    /// <summary>
    /// Unidades disponíveis. <c>null</c> é estoque ilimitado — serviço, como banho e
    /// tosa, não acaba na prateleira (RN-109).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? Estoque { get; private set; }

    /// <summary>Visível e resgatável. Item inativo some do catálogo do Responsável.</summary>
    public bool Ativo { get; private set; }

    /// <summary>Momento do cadastro (UTC).</summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>Última alteração (UTC); nulo enquanto o item nunca foi alterado.</summary>
    public DateTime? AtualizadoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private ItemMarketplace()
    {
        Nome = null!;
        Fornecedor = null!;
    }

    /// <summary>Cadastra um item ativo no catálogo.</summary>
    /// <exception cref="ArgumentException">Nome ou fornecedor vazio.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Custo, valor ou estoque fora da faixa.</exception>
    public ItemMarketplace(
        string nome,
        string? descricao,
        CategoriaItem categoria,
        string fornecedor,
        int custoEmPontos,
        decimal valorDeReferencia,
        Guid? imagemMidiaId,
        int? estoque)
    {
        Id = Guid.NewGuid();
        Nome = null!;
        Fornecedor = null!;
        Aplicar(nome, descricao, categoria, fornecedor, custoEmPontos, valorDeReferencia, imagemMidiaId, estoque);
        Ativo = true;
        CriadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Disponível para resgate agora: ativo e com estoque (ou estoque ilimitado).
    /// </summary>
    public bool Disponivel => Ativo && Estoque is null or > 0;

    /// <summary>Altera os dados do item, com as mesmas validações do cadastro.</summary>
    public void Atualizar(
        string nome,
        string? descricao,
        CategoriaItem categoria,
        string fornecedor,
        int custoEmPontos,
        decimal valorDeReferencia,
        Guid? imagemMidiaId,
        int? estoque)
    {
        Aplicar(nome, descricao, categoria, fornecedor, custoEmPontos, valorDeReferencia, imagemMidiaId, estoque);
        AtualizadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Tira o item do catálogo. Idempotente: desativar um item inativo não é erro,
    /// porque o resultado pedido — item fora do catálogo — já é verdade.
    /// </summary>
    public void Desativar()
    {
        if (!Ativo)
            return;

        Ativo = false;
        AtualizadoEm = DateTime.UtcNow;
    }

    /// <summary>Devolve o item ao catálogo. Idempotente, pelo mesmo motivo de <see cref="Desativar"/>.</summary>
    public void Reativar()
    {
        if (Ativo)
            return;

        Ativo = true;
        AtualizadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Reserva uma unidade para um resgate (RN-109). Estoque ilimitado não muda.
    /// </summary>
    /// <exception cref="InvalidOperationException">Item inativo ou sem estoque (RN-108).</exception>
    public void ReservarUnidade()
    {
        if (!Disponivel)
            throw new InvalidOperationException("Item indisponivel para resgate.");

        if (Estoque is { } unidades)
            Estoque = unidades - 1;
    }

    /// <summary>
    /// Devolve a unidade de um cupom que venceu sem uso (RN-109). Os pontos não
    /// voltam (RN-053) — mas a unidade sim: ela nunca saiu da prateleira, e mantê-la
    /// reservada para sempre faria o estoque encolher a cada cupom esquecido.
    /// </summary>
    public void DevolverUnidade()
    {
        if (Estoque is { } unidades)
            Estoque = unidades + 1;
    }

    private void Aplicar(
        string nome,
        string? descricao,
        CategoriaItem categoria,
        string fornecedor,
        int custoEmPontos,
        decimal valorDeReferencia,
        Guid? imagemMidiaId,
        int? estoque)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("O nome do item e obrigatorio.", nameof(nome));

        if (string.IsNullOrWhiteSpace(fornecedor))
            throw new ArgumentException("O fornecedor do item e obrigatorio.", nameof(fornecedor));

        if (!Enum.IsDefined(categoria))
            throw new ArgumentOutOfRangeException(nameof(categoria), "Categoria de item desconhecida.");

        if (custoEmPontos <= 0)
            throw new ArgumentOutOfRangeException(nameof(custoEmPontos), "O custo em pontos deve ser positivo.");

        if (valorDeReferencia < 0)
            throw new ArgumentOutOfRangeException(nameof(valorDeReferencia), "O valor de referencia nao pode ser negativo.");

        if (estoque < 0)
            throw new ArgumentOutOfRangeException(nameof(estoque), "O estoque nao pode ser negativo.");

        Nome = nome.Trim();

        // Texto vazio vira nulo: no Oracle string vazia ja e NULL, e o InMemory dos
        // testes precisa enxergar a mesma coisa
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        Categoria = categoria;
        Fornecedor = fornecedor.Trim();
        CustoEmPontos = custoEmPontos;
        ValorDeReferencia = valorDeReferencia;
        ImagemMidiaId = imagemMidiaId;
        Estoque = estoque;
    }
}
