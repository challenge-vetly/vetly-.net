using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Domain.Entities;

/// <summary>
/// Cupom emitido na troca de pontos por um item do marketplace (RN-053/RN-054).
///
/// O cupom é <b>real</b> mesmo com o marketplace mockado: os pontos saem do saldo de
/// verdade, a divisão do custo entre Vetly e prestador é gravada de verdade, e o
/// código QR existe. O que é mock é a validação no estabelecimento (RN-019) — por
/// isso o item viaja como texto (<see cref="ItemRef"/>, <see cref="ItemNome"/>,
/// <see cref="Categoria"/>) e não como chave estrangeira: a taxonomia da RN-099 fica
/// preservada para quando `TB_ITEM_MARKETPLACE` existir.
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

    /// <summary>Referência do item no catálogo mockado do front (RN-098).</summary>
    [Required]
    [MaxLength(120)]
    public string ItemRef { get; private set; }

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
    public CupomResgate(
        Guid tutorId,
        string itemRef,
        string? itemNome,
        CategoriaItem categoria,
        int pontosDebitados,
        decimal desconto)
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
    /// Marca o cupom como usado. No MVP não há leitor no app do vet (RN-019, C3): a
    /// transição existe para que a validação real seja só ligar a tela, sem mexer no
    /// domínio.
    /// </summary>
    public void Resgatar(DateTime agora)
    {
        if (!Vigente(agora))
            throw new InvalidOperationException("Somente cupom vigente pode ser resgatado.");

        Status = StatusCupom.Resgatado;
        ResgatadoEm = agora;
    }

    /// <summary>
    /// Marca o cupom como vencido. Os pontos <b>não</b> voltam ao saldo (RN-053).
    /// </summary>
    public void Expirar()
    {
        if (Status == StatusCupom.Emitido)
            Status = StatusCupom.Expirado;
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
