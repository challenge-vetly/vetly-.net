using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Um lançamento no extrato de pontos do Responsável (RN-051/RN-052).
///
/// O saldo é a <b>soma dos lançamentos</b>, e não um campo que alguém atualiza. Saldo
/// guardado à parte diverge do extrato no primeiro erro, e aí não há como saber qual
/// dos dois está certo — o Responsável vê um número e a conferência mostra outro.
///
/// A tabela é append-only: pontos não são editados nem apagados. Corrigir um crédito
/// indevido é lançar o débito correspondente, do mesmo jeito que em contabilidade.
/// </summary>
public class MovimentoDePontos
{
    /// <summary>
    /// Pontos por real gasto em consulta realizada (RN-052). Um por real mantém a
    /// conta legível para quem recebe: R$ 180 viram 180 pontos.
    /// </summary>
    public const int PontosPorReal = 1;

    /// <summary>
    /// Quanto vale um ponto no resgate (RN-051). Cem pontos = R$ 1,00, ou seja, o
    /// retorno é de 1% do que foi gasto.
    /// </summary>
    public const decimal ReaisPorPonto = 0.01m;

    /// <summary>
    /// Mínimo para resgatar. Abaixo disso o desconto sairia em centavos, o que gera
    /// mais confusão no extrato do que valor para quem resgata.
    /// </summary>
    public const int MinimoParaResgate = 100;

    /// <summary>Validade do crédito. Ponto que nunca expira vira passivo eterno.</summary>
    public static readonly TimeSpan ValidadeDoCredito = TimeSpan.FromDays(365);

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

    /// <summary>Consulta que originou o lançamento, quando houve uma.</summary>
    public Guid? ConsultaId { get; private set; }

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
        Descricao = descricao;
        OcorridoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Credita pontos por uma consulta realizada e paga (RN-052).
    ///
    /// Só o valor efetivamente cobrado gera ponto: consulta cancelada ou com pagamento
    /// recusado não vira crédito, senão o programa pagaria por receita que não entrou.
    /// </summary>
    public static MovimentoDePontos PorConsulta(Guid tutorId, Guid consultaId, decimal valorPago)
    {
        if (valorPago <= 0)
            throw new ArgumentOutOfRangeException(nameof(valorPago),
                "Só valor efetivamente cobrado gera pontos.");

        var pontos = (int)Math.Floor(valorPago * PontosPorReal);

        var movimento = new MovimentoDePontos(
            tutorId, TipoMovimentoDePontos.Credito, pontos,
            $"Consulta realizada - R$ {valorPago:N2}")
        {
            ConsultaId = consultaId
        };

        movimento.ExpiraEm = movimento.OcorridoEm.Add(ValidadeDoCredito);

        return movimento;
    }

    /// <summary>
    /// Debita pontos usados como desconto (RN-051). O lançamento guarda quanto virou
    /// dinheiro, porque é isso que a conferência financeira precisa cruzar.
    /// </summary>
    public static MovimentoDePontos PorResgate(Guid tutorId, int pontos, decimal valorEmReais, Guid? pagamentoId)
    {
        if (pontos < MinimoParaResgate)
            throw new ArgumentOutOfRangeException(nameof(pontos),
                $"O resgate mínimo é de {MinimoParaResgate} pontos.");

        return new MovimentoDePontos(
            tutorId, TipoMovimentoDePontos.Debito, -pontos,
            $"Desconto aplicado - R$ {valorEmReais:N2}")
        {
            PagamentoId = pagamentoId,
            ValorEmReais = valorEmReais
        };
    }

    /// <summary>
    /// Baixa um crédito vencido. O extrato mostra a expiração em vez de o saldo cair
    /// sozinho, e o lançamento aponta para o crédito que baixou — é assim que a rotina
    /// sabe o que já processou, sem precisar alterar o crédito original. Tabela
    /// append-only não tem coluna de "já tratado".
    /// </summary>
    public static MovimentoDePontos PorExpiracao(Guid tutorId, int pontos, Guid creditoOrigemId) =>
        new(tutorId, TipoMovimentoDePontos.Expiracao, -pontos, "Pontos expirados")
        {
            MovimentoOrigemId = creditoOrigemId
        };

    /// <summary>Ajuste manual da operação. Fica no extrato como qualquer outro lançamento.</summary>
    public static MovimentoDePontos PorAjuste(Guid tutorId, int pontos, string motivo) =>
        new(tutorId, TipoMovimentoDePontos.Ajuste, pontos, motivo);

    /// <summary>Converte pontos em reais (RN-051).</summary>
    public static decimal EmReais(int pontos) => Math.Round(pontos * ReaisPorPonto, 2);

    /// <summary>Quantos pontos são necessários para um desconto de determinado valor.</summary>
    public static int PontosPara(decimal reais) => (int)Math.Ceiling(reais / ReaisPorPonto);
}
