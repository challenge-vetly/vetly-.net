using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Domain.Entities;

/// <summary>
/// Cupom emitido na troca de pontos por um item do marketplace (RN-053/RN-054).
///
/// Os pontos saem do saldo de verdade, a divisão do custo entre Vetly e prestador é
/// gravada de verdade, e o código QR é validado no estabelecimento por
/// <c>POST /api/marketplace/cupons/validar</c> (RN-110).
///
/// Desde a Sprint 4 o item vem do catálogo (<see cref="ItemMarketplace"/>) e o cupom
/// guarda <see cref="ItemMarketplaceId"/>. Os campos de texto (<see cref="ItemRef"/>,
/// <see cref="ItemNome"/>, <see cref="Categoria"/>) continuam gravados: são a foto do
/// item no momento do resgate — se o catálogo mudar o nome depois, o cupom continua
/// dizendo o que o Responsável trocou — e é o que existe nos cupons anteriores ao
/// catálogo, emitidos com o item escolhido pelo front.
///
/// Vencido o prazo, os pontos <b>não</b> voltam ao saldo (RN-053). É o que evita
/// passivo perpétuo e resgate especulativo — e por isso o Responsável é avisado da
/// validade na emissão e por push antes do vencimento.
/// </summary>
public class CupomResgate
{
    /// <summary>Identificador do cupom (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Responsável dono do cupom.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>Código apresentado no estabelecimento, renderizado como QR pelo app.</summary>
    [Required]
    [MaxLength(40)]
    public string CodigoQr { get; private set; }

    /// <summary>
    /// Referência do item. No resgate pelo catálogo é o id do
    /// <see cref="ItemMarketplace"/>; nos cupons anteriores, o texto que o front
    /// mandava (RN-098).
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string ItemRef { get; private set; }

    /// <summary>
    /// Item do catálogo resgatado (RN-108). Nulo nos cupons emitidos antes do
    /// catálogo existir. É por ele que um cupom vencido devolve a unidade ao estoque
    /// (RN-109).
    /// </summary>
    public Guid? ItemMarketplaceId { get; private set; }

    [MaxLength(200)]
    public string? ItemNome { get; private set; }

    /// <summary>Categoria na taxonomia que sustenta a taxa de listagem (RN-099).</summary>
    [Required]
    public CategoriaItem Categoria { get; private set; }

    /// <summary>Pontos debitados do saldo.</summary>
    public int PontosDebitados { get; private set; }

    /// <summary>Valor do desconto em reais (RN-049).</summary>
    public decimal Desconto { get; private set; }

    /// <summary>Faixa que definiu a divisão do custo (RN-051).</summary>
    [Required]
    public FaixaDeFinanciamento Faixa { get; private set; }

    /// <summary>Parte do desconto absorvida pela Vetly (RN-051).</summary>
    public decimal DescontoVetly { get; private set; }

    /// <summary>Parte do desconto absorvida pelo prestador (RN-051).</summary>
    public decimal DescontoPrestador { get; private set; }

    [Required]
    public StatusCupom Status { get; private set; }

    public DateTime EmitidoEm { get; private set; }
    public DateTime ExpiraEm { get; private set; }
    public DateTime? ResgatadoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private CupomResgate()
    {
        CodigoQr = null!;
        ItemRef = null!;
    }

    /// <summary>Emite o cupom a partir de um resgate (RN-053).</summary>
    /// <param name="tutorId">Responsável dono do cupom.</param>
    /// <param name="itemRef">Referência do item (o id, no resgate pelo catálogo).</param>
    /// <param name="itemNome">Nome do item no momento do resgate.</param>
    /// <param name="categoria">Categoria do item (RN-099).</param>
    /// <param name="pontosDebitados">Pontos que saíram do saldo.</param>
    /// <param name="desconto">Valor dos pontos em reais (RN-049).</param>
    /// <param name="itemMarketplaceId">Item do catálogo; nulo no resgate legado.</param>
    public CupomResgate(
        Guid tutorId,
        string itemRef,
        string? itemNome,
        CategoriaItem categoria,
        int pontosDebitados,
        decimal desconto,
        Guid? itemMarketplaceId = null)
    {
        if (string.IsNullOrWhiteSpace(itemRef))
            throw new ArgumentException("O item do resgate é obrigatório.", nameof(itemRef));

        if (pontosDebitados <= 0)
            throw new ArgumentOutOfRangeException(nameof(pontosDebitados),
                "O resgate deve debitar pontos.");

        var (vetly, prestador, faixa) = RegrasDeFidelidade.Dividir(desconto);

        Id = Guid.NewGuid();
        TutorId = tutorId;
        CodigoQr = GerarCodigo();
        ItemRef = itemRef.Trim();
        ItemMarketplaceId = itemMarketplaceId;
        ItemNome = itemNome?.Trim();
        Categoria = categoria;
        PontosDebitados = pontosDebitados;
        Desconto = desconto;
        Faixa = faixa;
        DescontoVetly = vetly;
        DescontoPrestador = prestador;
        Status = StatusCupom.Emitido;
        EmitidoEm = DateTime.UtcNow;
        ExpiraEm = EmitidoEm.Add(RegrasDeFidelidade.ValidadeDoCupom);
    }

    /// <summary>Verdadeiro enquanto o cupom pode ser apresentado.</summary>
    public bool Vigente(DateTime agora) => Status == StatusCupom.Emitido && ExpiraEm > agora;

    /// <summary>
    /// Marca o cupom como usado: é a validação do QR no estabelecimento (RN-110) e o
    /// consumo do cupom aplicado a uma cobrança (RN-054).
    /// </summary>
    public void Resgatar(DateTime agora)
    {
        if (!Vigente(agora))
            throw new InvalidOperationException("Somente cupom vigente pode ser resgatado.");

        Status = StatusCupom.Resgatado;
        ResgatadoEm = agora;
    }

    /// <summary>
    /// Devolve o cupom à vigência depois de uma cobrança que não se concretizou
    /// (RN-053).
    ///
    /// Só faz sentido sobre um cupom resgatado: um expirado venceu por tempo, e
    /// ressuscitá-lo estenderia a validade por um erro que não foi do Responsável —
    /// mas também não foi da plataforma. A validade original é preservada de
    /// propósito: se ela já passou, a próxima leitura o trata como vencido.
    /// </summary>
    public void ReverterUso()
    {
        if (Status != StatusCupom.Resgatado)
            return;

        Status = StatusCupom.Emitido;
        ResgatadoEm = null;
    }

    /// <summary>
    /// Marca o cupom como vencido. Os pontos <b>não</b> voltam ao saldo (RN-053).
    /// </summary>
    /// <returns>
    /// Verdadeiro só na transição. É o que garante que a unidade do catálogo volte ao
    /// estoque uma única vez (RN-109), mesmo que a rotina e a validação do QR
    /// encontrem o mesmo cupom vencido.
    /// </returns>
    public bool Expirar()
    {
        if (Status != StatusCupom.Emitido)
            return false;

        Status = StatusCupom.Expirado;
        return true;
    }

    /// <summary>
    /// Código curto, legível e sem ambiguidade visual: sem O/0 e sem I/1, porque o
    /// código é digitado à mão quando a câmera não lê o QR.
    /// </summary>
    private static string GerarCodigo()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        var bytes = Guid.NewGuid().ToByteArray();
        var codigo = new char[12];

        for (var i = 0; i < codigo.Length; i++)
            codigo[i] = alfabeto[bytes[i] % alfabeto.Length];

        return "VETLY-" + new string(codigo);
    }
}
