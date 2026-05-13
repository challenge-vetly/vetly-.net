using Vetly.Application.DTOs.Cancelamento;
using Vetly.Domain.Entities;

namespace Vetly.Application.Strategies.Cancelamento;

/// <summary>
/// Contrato do Strategy Pattern para políticas de cancelamento e reembolso.
/// O ConsultaService seleciona a strategy de menor prioridade que seja aplicável
/// ao horário de cancelamento em relação ao horário da consulta (RN-019/020/021).
/// </summary>
public interface ICancelamentoStrategy
{
    /// <summary>
    /// Ordem de verificação (menor = verificada primeiro).
    /// Reembolso integral tem prioridade 1, sem reembolso tem prioridade 3.
    /// </summary>
    int Prioridade { get; }

    /// <summary>
    /// Verifica se esta strategy é aplicável dado o horário da consulta e o momento do cancelamento.
    /// </summary>
    bool Aplicavel(DateTime horarioConsulta, DateTime horarioCancelamento);

    /// <summary>Executa o cancelamento e retorna o resultado com valores de reembolso e retenção.</summary>
    ResultadoCancelamentoDto Executar(Pagamento pagamento, decimal percentualRetencao);
}
