using Vetly.Application.DTOs.Fidelidade;
using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Programa de fidelidade: pontos por consulta realizada e desconto no resgate
/// (RN-051/RN-052).
/// </summary>
public interface IFidelidadeService
{
    /// <summary>Saldo do Responsável — a soma dos lançamentos, não um campo guardado.</summary>
    Task<SaldoDePontosDto> ObterSaldoAsync(Guid tutorId);

    /// <summary>Extrato de pontos, do mais recente ao mais antigo.</summary>
    Task<IEnumerable<MovimentoDePontosDto>> ObterExtratoAsync(Guid tutorId);

    /// <summary>
    /// Credita pontos por uma consulta realizada e paga (RN-052). Devolve nulo quando
    /// não há o que creditar — consulta não realizada, pagamento não confirmado, ou
    /// crédito já lançado.
    /// </summary>
    Task<MovimentoDePontosDto?> CreditarPorConsultaAsync(Guid consultaId);

    /// <summary>
    /// Apura o desconto de um resgate sobre uma cobrança, sem gravar nada (RN-051).
    ///
    /// O <paramref name="teto"/> é o limite do desconto — na prática, a comissão da
    /// plataforma naquela cobrança. A Vetly banca a própria fidelidade, mas não paga
    /// para atender, e o prestador recebe o repasse cheio de todo jeito.
    /// </summary>
    Task<DescontoAplicadoDto> ApurarDescontoAsync(
        Guid tutorId, int pontos, decimal valorDaCobranca, decimal teto);

    /// <summary>Lança o débito do resgate depois que a cobrança foi criada (RN-051).</summary>
    Task RegistrarResgateAsync(Guid tutorId, int pontos, decimal valorEmReais, Guid pagamentoId);

    /// <summary>Baixa os créditos vencidos. Devolve quantos pontos expiraram.</summary>
    Task<int> ExpirarVencidosAsync();
}

/// <summary>
/// Repositório do extrato de pontos (RN-051/RN-052).
///
/// Append-only por contrato: só adicionar e ler. Saldo é a soma dos lançamentos, e um
/// extrato que pode ser reescrito não sustenta o saldo que mostra.
/// </summary>
public interface IFidelidadeRepository
{
    Task<IEnumerable<MovimentoDePontos>> ObterDoTutorAsync(Guid tutorId);

    /// <summary>Crédito já lançado para uma consulta, se houver. É como se evita creditar duas vezes.</summary>
    Task<MovimentoDePontos?> ObterCreditoDaConsultaAsync(Guid consultaId);

    /// <summary>
    /// Créditos vencidos que ainda não tiveram lançamento de baixa. A ausência da baixa
    /// é o que marca o que falta processar — a tabela não tem coluna de "já tratado".
    /// </summary>
    Task<IEnumerable<MovimentoDePontos>> ObterCreditosVencidosSemBaixaAsync(DateTime agora);

    Task AdicionarAsync(MovimentoDePontos movimento);

    Task<int> SalvarAsync();
}
