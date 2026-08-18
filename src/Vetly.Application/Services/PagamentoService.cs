using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Comissao;
using Vetly.Application.Strategies.Split;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de pagamentos. Processa criacao, split financeiro por persona (Strategy) e
/// comissao por plano do veterinario (Strategy — RN-089). No MVP todo pagamento é
/// simulado: nenhum valor real transita, apenas registrado (RN-037).
/// </summary>
public class PagamentoService : IPagamentoService
{
    private readonly IPagamentoRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IFidelidadeService _fidelidadeService;
    private readonly IEnumerable<ISplitFinanceiroStrategy> _splitStrategies;
    private readonly IEnumerable<IComissaoStrategy> _comissaoStrategies;
    private readonly TimeProvider _timeProvider;

    public PagamentoService(
        IPagamentoRepository repo,
        IVeterinarioRepository vetRepo,
        IConsultaRepository consultaRepo,
        IFidelidadeService fidelidadeService,
        IEnumerable<ISplitFinanceiroStrategy> splitStrategies,
        IEnumerable<IComissaoStrategy> comissaoStrategies,
        TimeProvider timeProvider)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _consultaRepo = consultaRepo;
        _fidelidadeService = fidelidadeService;
        _splitStrategies = splitStrategies;
        _comissaoStrategies = comissaoStrategies;
        _timeProvider = timeProvider;
    }

    public async Task<IEnumerable<PagamentoDto>> ObterTodosAsync()
    {
        var pagamentos = await _repo.ObterTodosAsync();
        return pagamentos.Select(MapearParaDto);
    }

    public async Task<PagamentoDto> ObterPorIdAsync(Guid id)
    {
        var pagamento = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Pagamento", id);
        return MapearParaDto(pagamento);
    }

    public async Task<PagamentoDto> CriarAsync(CriarPagamentoDto dto)
    {
        var pagamento = new Pagamento(dto.ResponsavelId, dto.Valor, dto.MeioPagamento, dto.ConsultaId, dto.InternacaoId);
        pagamento.Confirmar(); // pagamento confirmado ao criar (integracao com gateway real seria assincrona)
        await _repo.AdicionarAsync(pagamento);
        await _repo.SalvarAsync();
        return MapearParaDto(pagamento);
    }

    /// <summary>
    /// Processa o split financeiro por persona (RN-012..018) e, se o pagamento estiver
    /// vinculado a uma consulta, também aplica a comissão por plano (RN-089) — mantém o
    /// endpoint v1 compatível mesmo para pagamentos criados fora do fluxo de simulação.
    /// </summary>
    public async Task<PagamentoDto> ProcessarSplitAsync(Guid id)
    {
        var pagamento = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Pagamento", id);

        // Recupera o veterinario via consulta vinculada
        if (pagamento.ConsultaId is null)
            throw new BusinessRuleException("PAGAMENTO-001", "Split so pode ser processado para pagamentos vinculados a consultas.");

        var consulta = await _consultaRepo.ObterPorIdAsync(pagamento.ConsultaId.Value)
            ?? throw new NotFoundException("Consulta", pagamento.ConsultaId.Value);

        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", consulta.VeterinarioId);

        var splitStrategy = _splitStrategies.First(s => s.Aplicavel(vet));
        var percentualSplit = splitStrategy.CalcularPercentualVeterinario(vet, pagamento);
        pagamento.DefinirSplit(percentualSplit);

        AplicarComissao(pagamento, vet);

        _repo.Atualizar(pagamento);
        await _repo.SalvarAsync();
        return MapearParaDto(pagamento);
    }

    /// <inheritdoc/>
    public async Task<SimularPagamentoResponseDto> ProcessarSimuladoAsync(SimularPagamentoDto dto)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(dto.ConsultaId)
            ?? throw new NotFoundException("Consulta", dto.ConsultaId);

        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", consulta.VeterinarioId);

        var pagamento = new Pagamento(consulta.ResponsavelId, dto.Valor, dto.Meio, consulta.Id);
        pagamento.Confirmar(); // simulado — sempre retorna sucesso (RN-037)
        AplicarComissao(pagamento, vet);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        // RN-072: desconto de fidelidade calculado e exibido, sem abatimento real do valor.
        var desconto = await _fidelidadeService.CalcularDescontoAsync(consulta.ResponsavelId, dto.Valor, agora);
        pagamento.RegistrarDescontoFidelidade(desconto.ValorDesconto, desconto.IncidenciaVetly, desconto.IncidenciaVeterinario);

        await _repo.AdicionarAsync(pagamento);
        await _repo.SalvarAsync();

        consulta.ConfirmarPagamento(agora);
        _consultaRepo.Atualizar(consulta);
        await _consultaRepo.SalvarAsync();

        return new SimularPagamentoResponseDto
        {
            Id = pagamento.Id, Status = pagamento.StatusPagamento, Simulado = pagamento.Simulado,
            PercentualComissao = pagamento.PercentualComissao, ValorComissao = pagamento.ValorComissao,
            ValorRepasse = pagamento.ValorRepasse, ConsultaStatus = consulta.Status,
            DescontoFidelidadeCalculado = pagamento.DescontoFidelidadeCalculado,
            IncidenciaVetly = pagamento.IncidenciaVetly, IncidenciaVeterinario = pagamento.IncidenciaVeterinario
        };
    }

    /// <summary>Seleciona a IComissaoStrategy pelo plano do veterinário e grava o resultado no pagamento (RN-089).</summary>
    private void AplicarComissao(Pagamento pagamento, Veterinario vet)
    {
        var strategy = _comissaoStrategies.FirstOrDefault(s => s.Aplicavel(vet.Plano))
            ?? throw new BusinessRuleException("PAGAMENTO-002",
                $"Nenhuma estrategia de comissao registrada para o plano '{vet.Plano}'.");

        pagamento.RegistrarComissao(strategy.PercentualComissao);
    }

    private static PagamentoDto MapearParaDto(Pagamento p) => new()
    {
        Id = p.Id, ResponsavelId = p.ResponsavelId, ConsultaId = p.ConsultaId, InternacaoId = p.InternacaoId,
        Valor = p.Valor, MeioPagamento = p.MeioPagamento, Momento = p.Momento,
        StatusPagamento = p.StatusPagamento, PercentualSplit = p.PercentualSplit,
        ValorEstornado = p.ValorEstornado, Simulado = p.Simulado,
        PercentualComissao = p.PercentualComissao, ValorComissao = p.ValorComissao,
        ValorRepasse = p.ValorRepasse, DescontoFidelidadeCalculado = p.DescontoFidelidadeCalculado,
        IncidenciaVetly = p.IncidenciaVetly, IncidenciaVeterinario = p.IncidenciaVeterinario
    };
}
