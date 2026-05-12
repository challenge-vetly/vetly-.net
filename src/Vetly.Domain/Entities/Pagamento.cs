using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

public class Pagamento
{
    public Guid Id { get; private set; }
    public Guid TutorId { get; private set; }
    public Guid? ConsultaId { get; private set; }
    public Guid? InternacaoId { get; private set; }
    public decimal Valor { get; private set; }
    public MeioPagamento MeioPagamento { get; private set; }
    public DateTime Momento { get; private set; }
    public StatusPagamento StatusPagamento { get; private set; }
    public decimal PercentualSplit { get; private set; }
    public decimal? ValorEstornado { get; private set; }

    private Pagamento() { }

    public Pagamento(Guid tutorId, decimal valor, MeioPagamento meio, Guid? consultaId = null, Guid? internacaoId = null)
    {
        Id = Guid.NewGuid();
        TutorId = tutorId;
        Valor = valor;
        MeioPagamento = meio;
        ConsultaId = consultaId;
        InternacaoId = internacaoId;
        Momento = DateTime.UtcNow;
        StatusPagamento = StatusPagamento.Pendente;
    }

    public void Confirmar() => StatusPagamento = StatusPagamento.Confirmado;

    public void DefinirSplit(decimal percentual) => PercentualSplit = percentual;

    public void Estornar(decimal valor)
    {
        ValorEstornado = valor;
        StatusPagamento = valor >= Valor ? StatusPagamento.Estornado : StatusPagamento.Parcial;
    }
}
