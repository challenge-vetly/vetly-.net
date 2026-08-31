using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Fidelidade;

/// <summary>Saldo de pontos do Responsável (RN-051/RN-052).</summary>
public class SaldoDePontosDto
{
    public Guid TutorId { get; set; }

    /// <summary>Soma dos lançamentos. Não é campo guardado: é o extrato somado.</summary>
    public int Saldo { get; set; }

    /// <summary>Quanto o saldo vale em desconto.</summary>
    public decimal ValorEmReais { get; set; }

    /// <summary>
    /// Pontos que vencem nos próximos 30 dias. Avisar antes é o que separa o programa
    /// de fidelidade de uma pegadinha.
    /// </summary>
    public int PontosVencendoEm30Dias { get; set; }

    public int MinimoParaResgate { get; set; }
    public bool PodeResgatar { get; set; }
}

/// <summary>Um lançamento no extrato de pontos (RN-051/RN-052).</summary>
public class MovimentoDePontosDto
{
    public Guid Id { get; set; }
    public TipoMovimentoDePontos Tipo { get; set; }

    /// <summary>Positivo em crédito, negativo em débito e expiração.</summary>
    public int Pontos { get; set; }

    public Guid? ConsultaId { get; set; }
    public Guid? PagamentoId { get; set; }

    /// <summary>Valor em reais do desconto concedido, no débito.</summary>
    public decimal? ValorEmReais { get; set; }

    public DateTime? ExpiraEm { get; set; }
    public string? Descricao { get; set; }
    public DateTime OcorridoEm { get; set; }
}

/// <summary>Resultado do resgate aplicado a uma cobrança (RN-051).</summary>
public class DescontoAplicadoDto
{
    public int PontosResgatados { get; set; }
    public decimal ValorDoDesconto { get; set; }

    /// <summary>Valor que o Responsável de fato paga.</summary>
    public decimal ValorFinal { get; set; }
}
