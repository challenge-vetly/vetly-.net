using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Fidelidade;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de fidelidade. Orquestra a pontuação de consultas realizadas (RN-070/075), o
/// estorno antifraude (RN-075), o recálculo de tier (RN-071) e o desconto por tier via
/// Strategy (RN-072).
/// </summary>
public class FidelidadeService : IFidelidadeService
{
    // Pesos de pontuação (RN-070): a spec não fixa valores exatos, só a proporção
    // "cumprir obrigação pontua mais que consulta avulsa" — decisão de fundação da Fase 10.
    private const int PontosObrigacaoCumprida = 50;
    private const int PontosConsultaAvulsa = 20;

    private readonly IPontosFidelidadeRepository _pontosRepo;
    private readonly IObrigacaoDoPetRepository _obrigacaoRepo;
    private readonly IResponsavelRepository _responsavelRepo;
    private readonly IEnumerable<IDescontoFidelidadeStrategy> _descontoStrategies;
    private readonly TimeProvider _timeProvider;

    public FidelidadeService(
        IPontosFidelidadeRepository pontosRepo, IObrigacaoDoPetRepository obrigacaoRepo,
        IResponsavelRepository responsavelRepo, IEnumerable<IDescontoFidelidadeStrategy> descontoStrategies,
        TimeProvider timeProvider)
    {
        _pontosRepo = pontosRepo;
        _obrigacaoRepo = obrigacaoRepo;
        _responsavelRepo = responsavelRepo;
        _descontoStrategies = descontoStrategies;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task PontuarConsultaRealizadaAsync(
        Guid consultaId, Guid animalId, Guid responsavelId, TipoServico tipoServico, DateTime agora)
    {
        var tipoObrigacao = MapearParaTipoObrigacao(tipoServico);
        var obrigacao = tipoObrigacao is { } tipo
            ? await _obrigacaoRepo.ObterPendenteMaisProximaAsync(animalId, tipo)
            : null;

        int pontos;
        OrigemPontos origem;
        if (obrigacao is not null && obrigacao.EstaNoPrazo(agora))
        {
            obrigacao.MarcarCumprida(consultaId, agora);
            _obrigacaoRepo.Atualizar(obrigacao);
            pontos = PontosObrigacaoCumprida;
            origem = OrigemPontos.ObrigacaoCumprida;
        }
        else
        {
            pontos = PontosConsultaAvulsa;
            origem = OrigemPontos.ConsultaAvulsa;
        }

        var lancamento = new PontosFidelidade(responsavelId, consultaId, origem, pontos, agora);
        await _pontosRepo.AdicionarAsync(lancamento);
        await _pontosRepo.SalvarAsync();

        await RecalcularFidelidadeAsync(responsavelId, agora);
    }

    /// <inheritdoc/>
    public async Task EstornarPontosPorCancelamentoAsync(Guid consultaId, DateTime agora)
    {
        var lancamento = await _pontosRepo.ObterPorConsultaAsync(consultaId);
        if (lancamento is null || lancamento.Estornado)
            return;

        lancamento.Estornar();
        _pontosRepo.Atualizar(lancamento);
        await _pontosRepo.SalvarAsync();

        await RecalcularFidelidadeAsync(lancamento.ResponsavelId, agora);
    }

    /// <inheritdoc/>
    public async Task<ResultadoDescontoFidelidadeDto> CalcularDescontoAsync(Guid responsavelId, decimal valorServico, DateTime agora)
    {
        var responsavel = await _responsavelRepo.ObterPorIdAsync(responsavelId)
            ?? throw new NotFoundException("Responsavel", responsavelId);

        // RN-064: penalidade de no-show zera o desconto, mesmo com tier elegível.
        if (responsavel.BloqueadoDescontosAte is { } bloqueadoAte && agora <= bloqueadoAte)
            return new ResultadoDescontoFidelidadeDto
            {
                TierFidelidade = responsavel.TierFidelidade, PercentualDesconto = 0,
                ValorDesconto = 0, IncidenciaVetly = 0, IncidenciaVeterinario = 0,
                BloqueadoPorPenalidade = true
            };

        var strategy = _descontoStrategies.First(s => s.Aplicavel(responsavel.TierFidelidade));

        return new ResultadoDescontoFidelidadeDto
        {
            TierFidelidade = responsavel.TierFidelidade,
            PercentualDesconto = strategy.PercentualDesconto,
            ValorDesconto = Math.Round(valorServico * strategy.PercentualDesconto / 100m, 2),
            IncidenciaVetly = Math.Round(valorServico * strategy.PercentualIncidenciaVetly / 100m, 2),
            IncidenciaVeterinario = Math.Round(valorServico * strategy.PercentualIncidenciaVeterinario / 100m, 2),
            BloqueadoPorPenalidade = false
        };
    }

    /// <inheritdoc/>
    public async Task<FidelidadeDto> ObterFidelidadeAsync(Guid responsavelId)
    {
        var responsavel = await _responsavelRepo.ObterPorIdAsync(responsavelId)
            ?? throw new NotFoundException("Responsavel", responsavelId);

        int? faltamParaProximoTier = responsavel.TierFidelidade switch
        {
            TierFidelidade.Bronze => 300 - responsavel.SaldoPontos,
            TierFidelidade.Prata => 800 - responsavel.SaldoPontos,
            _ => null
        };

        return new FidelidadeDto
        {
            ResponsavelId = responsavel.Id, TierFidelidade = responsavel.TierFidelidade,
            SaldoPontos = responsavel.SaldoPontos, PontosParaProximoTier = faltamParaProximoTier
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PontosFidelidadeDto>> ObterExtratoAsync(Guid responsavelId)
    {
        _ = await _responsavelRepo.ObterPorIdAsync(responsavelId)
            ?? throw new NotFoundException("Responsavel", responsavelId);

        var lancamentos = await _pontosRepo.ObterPorResponsavelAsync(responsavelId);
        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        return lancamentos.Select(p => MapearParaDto(p, agora));
    }

    private async Task RecalcularFidelidadeAsync(Guid responsavelId, DateTime agora)
    {
        var responsavel = await _responsavelRepo.ObterPorIdAsync(responsavelId)
            ?? throw new NotFoundException("Responsavel", responsavelId);

        var lancamentos = await _pontosRepo.ObterPorResponsavelAsync(responsavelId);
        var saldoValido = lancamentos.Where(p => p.Valido(agora)).Sum(p => p.Pontos);

        responsavel.RecalcularFidelidade(saldoValido);
        _responsavelRepo.Atualizar(responsavel);
        await _responsavelRepo.SalvarAsync();
    }

    /// <summary>
    /// Mapeia o tipo de serviço da consulta para o tipo de obrigação que ela pode cumprir
    /// (RN-070). A spec não define esse mapeamento explicitamente — decisão de fundação da
    /// Fase 10: só Vacinacao/Retorno/Consulta/Teleorientacao casam com uma obrigação;
    /// Cirurgia/Exame nunca cumprem obrigação (pontuam sempre como avulsa).
    /// </summary>
    private static TipoObrigacao? MapearParaTipoObrigacao(TipoServico tipoServico) => tipoServico switch
    {
        TipoServico.Vacinacao => TipoObrigacao.Vacina,
        TipoServico.Retorno => TipoObrigacao.Retorno,
        TipoServico.Consulta => TipoObrigacao.CheckUp,
        TipoServico.Teleorientacao => TipoObrigacao.CheckUp,
        _ => null
    };

    private static PontosFidelidadeDto MapearParaDto(PontosFidelidade p, DateTime agora) => new()
    {
        Id = p.Id, ResponsavelId = p.ResponsavelId, ConsultaId = p.ConsultaId, Origem = p.Origem,
        Pontos = p.Pontos, Data = p.Data, ExpiraEm = p.ExpiraEm, Estornado = p.Estornado,
        Valido = p.Valido(agora)
    };
}
