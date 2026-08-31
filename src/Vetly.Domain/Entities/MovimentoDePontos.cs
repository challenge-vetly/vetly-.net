using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Domain.Entities;

/// <summary>
/// Um lançamento no extrato de pontos do Responsável (RN-047 a RN-052).
///
/// O saldo é a <b>soma dos lançamentos</b>, e não um campo que alguém atualiza. Saldo
/// guardado à parte diverge do extrato no primeiro erro, e aí não há como saber qual
/// dos dois está certo — o Responsável vê um número e a conferência mostra outro.
///
/// A tabela é append-only: pontos não são editados nem apagados. Corrigir um crédito
/// indevido é lançar o débito correspondente, do mesmo jeito que em contabilidade.
///
/// O crédito carrega <see cref="Restante"/> porque o consumo é <b>FIFO</b> (RN-050):
/// o resgate come primeiro o ponto mais antigo, que é o que está mais perto de
/// vencer. Sem controlar o saldo de cada lote, "expirar o que venceu" e "gastar o
/// mais velho" viram a mesma conta feita de dois jeitos incompatíveis.
/// </summary>
public class MovimentoDePontos
{
    /// <summary>Identificador do lançamento (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Responsável dono dos pontos.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>Natureza do lançamento.</summary>
    [Required]
    public TipoMovimentoDePontos Tipo { get; private set; }

    /// <summary>
    /// Pontos do lançamento. Positivo em crédito, negativo em débito e expiração —
    /// assim o saldo é literalmente a soma da coluna.
    /// </summary>
    public int Pontos { get; private set; }

    /// <summary>Pontos antes do multiplicador de tier (RN-047/RN-048).</summary>
    public int PontosBrutos { get; private set; }

    /// <summary>Multiplicador do tier vigente no momento do crédito (RN-048).</summary>
    public decimal Multiplicador { get; private set; }

    /// <summary>
    /// Quanto ainda resta deste lote de crédito. Só faz sentido em crédito, e é o que
    /// permite o consumo FIFO e a expiração por lote (RN-050).
    /// </summary>
    public int Restante { get; private set; }

    /// <summary>Consulta que originou o lançamento, quando houve uma.</summary>
    public Guid? ConsultaId { get; private set; }

    /// <summary>Obrigação do pet cumprida que originou o crédito (RN-047).</summary>
    public Guid? ObrigacaoId { get; private set; }

    /// <summary>Cupom que consumiu estes pontos, no débito (RN-053).</summary>
    public Guid? CupomId { get; private set; }

    /// <summary>Pagamento em que o desconto foi aplicado, no débito.</summary>
    public Guid? PagamentoId { get; private set; }

    /// <summary>Valor em reais do desconto concedido, no débito.</summary>
    public decimal? ValorEmReais { get; private set; }

    /// <summary>Quando o crédito perde a validade. Nulo em débito e expiração.</summary>
    public DateTime? ExpiraEm { get; private set; }

    /// <summary>
    /// Crédito que este lançamento baixou, na expiração. É o que permite à rotina
    /// saber o que já processou sem alterar o crédito original.
    /// </summary>
    public Guid? MovimentoOrigemId { get; private set; }

    /// <summary>O que aconteceu, em texto, para o extrato do Responsável.</summary>
    [MaxLength(200)]
    public string? Descricao { get; private set; }

    public DateTime OcorridoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private MovimentoDePontos() { }

    private MovimentoDePontos(
        Guid tutorId, TipoMovimentoDePontos tipo, int pontos, string? descricao)
    {
        Id = Guid.NewGuid();
        TutorId = tutorId;
        Tipo = tipo;
        Pontos = pontos;
        Multiplicador = 1.0m;
        Descricao = descricao;
        OcorridoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Credita pontos por um serviço pago: 1 ponto por real, arredondado para baixo
    /// (RN-047), com o multiplicador do tier vigente (RN-048).
    ///
    /// Só o valor efetivamente cobrado gera ponto: consulta cancelada ou com pagamento
    /// recusado não vira crédito, senão o programa pagaria por receita que não entrou
    /// (RN-052).
    /// </summary>
    public static MovimentoDePontos PorServicoPago(
        Guid tutorId, Guid consultaId, decimal valorPago, TierFidelidade tier)
    {
        if (valorPago <= 0)
            throw new ArgumentOutOfRangeException(nameof(valorPago),
                "Só valor efetivamente cobrado gera pontos.");

        var brutos = (int)Math.Floor(valorPago * RegrasDeFidelidade.PontosPorReal);

        return Creditar(tutorId, brutos, tier, $"Consulta realizada - R$ {valorPago:N2}",
            consultaId: consultaId);
    }

    /// <summary>
    /// Credita os 50 pontos fixos por obrigação do pet cumprida no prazo (RN-047).
    ///
    /// É o crédito que paga <b>comportamento</b>, e por isso independe do valor: manter
    /// a vacinação em dia vale o mesmo numa consulta de R$ 80 e numa de R$ 300.
    /// </summary>
    public static MovimentoDePontos PorObrigacaoCumprida(
        Guid tutorId, Guid obrigacaoId, string descricaoDaObrigacao, TierFidelidade tier) =>
        Creditar(tutorId, RegrasDeFidelidade.PontosPorObrigacaoCumprida, tier,
            $"{descricaoDaObrigacao} em dia", obrigacaoId: obrigacaoId);

    /// <summary>Aplica o multiplicador do tier e monta o lote de crédito.</summary>
    private static MovimentoDePontos Creditar(
        Guid tutorId, int brutos, TierFidelidade tier, string descricao,
        Guid? consultaId = null, Guid? obrigacaoId = null)
    {
        var multiplicador = RegrasDeFidelidade.MultiplicadorDe(tier);

        // Para baixo: o programa não credita ponto que não foi conquistado
        var creditados = (int)Math.Floor(brutos * multiplicador);

        var movimento = new MovimentoDePontos(
            tutorId, TipoMovimentoDePontos.Credito, creditados, descricao)
        {
            PontosBrutos = brutos,
            Multiplicador = multiplicador,
            Restante = creditados,
            ConsultaId = consultaId,
            ObrigacaoId = obrigacaoId
        };

        movimento.ExpiraEm = movimento.OcorridoEm.Add(RegrasDeFidelidade.ValidadeDosPontos);

        return movimento;
    }

    /// <summary>
    /// Debita pontos usados num resgate (RN-050). O lançamento guarda quanto virou
    /// dinheiro, porque é isso que a conferência financeira precisa cruzar.
    /// </summary>
    public static MovimentoDePontos PorResgate(
        Guid tutorId, int pontos, decimal valorEmReais, Guid cupomId)
    {
        if (pontos <= 0)
            throw new ArgumentOutOfRangeException(nameof(pontos), "O resgate deve debitar pontos.");

        return new MovimentoDePontos(
            tutorId, TipoMovimentoDePontos.Debito, -pontos,
            $"Resgate - R$ {valorEmReais:N2}")
        {
            CupomId = cupomId,
            ValorEmReais = valorEmReais
        };
    }

    /// <summary>
    /// Estorna os pontos de uma consulta cancelada ou reembolsada (RN-052).
    ///
    /// Estornar é lançar o oposto, não apagar o crédito: o extrato precisa mostrar que
    /// houve crédito e que ele foi desfeito, senão o Responsável vê o saldo cair sem
    /// explicação.
    /// </summary>
    public static MovimentoDePontos PorEstorno(Guid tutorId, int pontos, Guid consultaId) =>
        new(tutorId, TipoMovimentoDePontos.Estorno, -pontos, "Estorno por cancelamento")
        {
            ConsultaId = consultaId
        };

    /// <summary>
    /// Baixa um crédito vencido. O extrato mostra a expiração em vez de o saldo cair
    /// sozinho, e o lançamento aponta para o crédito que baixou — é assim que a rotina
    /// sabe o que já processou. Tabela append-only não tem coluna de "já tratado".
    /// </summary>
    public static MovimentoDePontos PorExpiracao(Guid tutorId, int pontos, Guid creditoOrigemId) =>
        new(tutorId, TipoMovimentoDePontos.Expiracao, -pontos, "Pontos expirados")
        {
            MovimentoOrigemId = creditoOrigemId
        };

    /// <summary>Ajuste manual da operação. Fica no extrato como qualquer outro lançamento.</summary>
    public static MovimentoDePontos PorAjuste(Guid tutorId, int pontos, string motivo) =>
        new(tutorId, TipoMovimentoDePontos.Ajuste, pontos, motivo);

    /// <summary>
    /// Consome parte deste lote no resgate FIFO (RN-050). Devolve quanto foi
    /// efetivamente consumido, que pode ser menos do que o pedido quando o lote acaba.
    /// </summary>
    public int Consumir(int pontos)
    {
        if (Tipo != TipoMovimentoDePontos.Credito)
            throw new InvalidOperationException("Somente lotes de crédito são consumidos.");

        var consumido = Math.Min(Restante, Math.Max(pontos, 0));
        Restante -= consumido;

        return consumido;
    }

    /// <summary>Verdadeiro quando o lote venceu e ainda tem saldo a baixar (RN-050).</summary>
    public bool VencidoEm(DateTime agora) =>
        Tipo == TipoMovimentoDePontos.Credito && Restante > 0 && ExpiraEm <= agora;
}
