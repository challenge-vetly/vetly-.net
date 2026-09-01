using Vetly.Application.DTOs.Fidelidade;
using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Programa de fidelidade (RN-046 a RN-054): pontos por serviço pago e por obrigação
/// cumprida, tier com multiplicador, resgate em cupom e expiração FIFO.
/// </summary>
public interface IFidelidadeService
{
    /// <summary>Saldo, tier e o que vence — a soma dos lançamentos, não um campo.</summary>
    Task<SaldoDePontosDto> ObterSaldoAsync(Guid tutorId);

    /// <summary>Extrato de pontos, do mais recente ao mais antigo.</summary>
    Task<IEnumerable<MovimentoDePontosDto>> ObterExtratoAsync(Guid tutorId);

    /// <summary>
    /// Credita 1 ponto por real do serviço pago, com o multiplicador do tier
    /// (RN-047/RN-048). Devolve nulo quando não há o que creditar.
    /// </summary>
    Task<MovimentoDePontosDto?> CreditarPorConsultaAsync(Guid consultaId);

    /// <summary>
    /// Credita os 50 pontos fixos da obrigação cumprida no prazo (RN-047). É o crédito
    /// que paga comportamento de cuidado, e não gasto.
    /// </summary>
    Task<MovimentoDePontosDto?> CreditarPorObrigacaoAsync(Guid tutorId, Guid obrigacaoId, string descricao);

    /// <summary>
    /// Calcula o desconto e a divisão Vetly ↔ prestador sem gravar nada
    /// (RN-017/RN-051). É o que o app mostra antes de o Responsável confirmar.
    /// </summary>
    Task<SimulacaoDeResgateDto> SimularResgateAsync(Guid tutorId, SimularResgateDto dto);

    /// <summary>
    /// Debita os pontos em FIFO, emite o cupom e grava a divisão da incidência
    /// (RN-018/RN-050/RN-053).
    /// </summary>
    Task<CupomDto> ResgatarAsync(Guid tutorId, SimularResgateDto dto);

    /// <summary>Cupons do Responsável, do mais recente ao mais antigo.</summary>
    Task<IEnumerable<CupomDto>> ObterCuponsAsync(Guid tutorId);

    /// <summary>Um cupom, com o código que o app renderiza como QR.</summary>
    Task<CupomDto> ObterCupomAsync(Guid cupomId);

    /// <summary>
    /// Marca o cupom como consumido depois de aplicado a uma cobrança (RN-054): um
    /// cupom vale para uma transação, e reaplicá-lo empilharia desconto sobre a
    /// mesma margem.
    /// </summary>
    Task MarcarCupomComoUsadoAsync(Guid cupomId);

    /// <summary>
    /// Devolve à vigência um cupom marcado como usado numa cobrança que não vingou
    /// (RN-053).
    ///
    /// Os pontos não voltam ao saldo — eles saíram no resgate, e é o cupom que os
    /// representa. O que volta é a possibilidade de usá-lo, dentro da validade que
    /// ele já tinha: o Responsável não perde o benefício porque o cartão foi recusado.
    /// </summary>
    Task ReverterUsoDoCupomAsync(Guid cupomId);

    /// <summary>
    /// Estorna os pontos de uma consulta cancelada ou reembolsada (RN-052). Devolve
    /// quantos pontos foram estornados.
    /// </summary>
    Task<int> EstornarPorConsultaAsync(Guid consultaId);

    /// <summary>Baixa créditos e cupons vencidos. Devolve quantos pontos expiraram.</summary>
    Task<int> ExpirarVencidosAsync();
}

/// <summary>
/// Repositório do extrato de pontos e dos cupons (RN-047 a RN-054).
///
/// O extrato é append-only, com uma exceção deliberada: <see cref="Atualizar"/> existe
/// para o campo <c>Restante</c> do lote, que é o mecanismo do FIFO. O <b>valor</b> do
/// lançamento nunca muda — só quanto dele já foi consumido.
/// </summary>
public interface IFidelidadeRepository
{
    Task<IEnumerable<MovimentoDePontos>> ObterDoTutorAsync(Guid tutorId);

    /// <summary>Lotes de crédito com saldo, para o consumo FIFO (RN-050).</summary>
    Task<IEnumerable<MovimentoDePontos>> ObterLotesComSaldoAsync(Guid tutorId);

    /// <summary>Crédito já lançado para uma consulta. É como se evita creditar duas vezes.</summary>
    Task<MovimentoDePontos?> ObterCreditoDaConsultaAsync(Guid consultaId);

    /// <summary>Estorno já lançado para uma consulta (RN-052).</summary>
    Task<MovimentoDePontos?> ObterEstornoDaConsultaAsync(Guid consultaId);

    /// <summary>Crédito já lançado para uma obrigação cumprida (RN-047).</summary>
    Task<MovimentoDePontos?> ObterCreditoDaObrigacaoAsync(Guid obrigacaoId);

    /// <summary>Lotes vencidos que ainda têm saldo a baixar (RN-050).</summary>
    Task<IEnumerable<MovimentoDePontos>> ObterCreditosVencidosSemBaixaAsync(DateTime agora);

    Task AdicionarAsync(MovimentoDePontos movimento);

    /// <summary>Persiste o consumo do lote. Só o <c>Restante</c> muda.</summary>
    void Atualizar(MovimentoDePontos movimento);

    Task<CupomResgate?> ObterCupomAsync(Guid cupomId);
    Task<IEnumerable<CupomResgate>> ObterCuponsDoTutorAsync(Guid tutorId);

    /// <summary>Cupons emitidos que passaram da validade (RN-053).</summary>
    Task<IEnumerable<CupomResgate>> ObterCuponsVencidosAsync(DateTime agora);

    Task AdicionarCupomAsync(CupomResgate cupom);
    void AtualizarCupom(CupomResgate cupom);

    Task<int> SalvarAsync();
}
