using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Split;
using Vetly.Domain.Entities;
using Vetly.Application.Interfaces;

namespace Vetly.Application.Services;

/// <summary>ServiÃ§o de pagamentos. Processa criaÃ§Ã£o e split financeiro via Strategy.</summary>
public class PagamentoService : IPagamentoService
{
    private readonly IPagamentoRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IEnumerable<ISplitFinanceiroStrategy> _splitStrategies;

    public PagamentoService(
        IPagamentoRepository repo,
        IVeterinarioRepository vetRepo,
        IConsultaRepository consultaRepo,
        IEnumerable<ISplitFinanceiroStrategy> splitStrategies)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _consultaRepo = consultaRepo;
        _splitStrategies = splitStrategies;
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
        var pagamento = new Pagamento(dto.TutorId, dto.Valor, dto.MeioPagamento, dto.ConsultaId, dto.InternacaoId);
        pagamento.Confirmar(); // pagamento confirmado ao criar (integraÃ§Ã£o com gateway real seria assÃ­ncrona)
        await _repo.AdicionarAsync(pagamento);
        await _repo.SalvarAsync();
        return MapearParaDto(pagamento);
    }

    /// <summary>
    /// Processa o split financeiro aplicando a Strategy correta com base na persona do veterinÃ¡rio.
    /// </summary>
    public async Task<PagamentoDto> ProcessarSplitAsync(Guid id)
    {
        var pagamento = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Pagamento", id);

        // Recupera o veterinÃ¡rio via consulta vinculada
        if (pagamento.ConsultaId is null)
            throw new BusinessRuleException("PAGAMENTO-001", "Split sÃ³ pode ser processado para pagamentos vinculados a consultas.");

        var consulta = await _consultaRepo.ObterPorIdAsync(pagamento.ConsultaId.Value)
            ?? throw new NotFoundException("Consulta", pagamento.ConsultaId.Value);

        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("VeterinÃ¡rio", consulta.VeterinarioId);

        var strategy = _splitStrategies.First(s => s.Aplicavel(vet));
        var percentual = strategy.CalcularPercentualVeterinario(vet, pagamento);

        pagamento.DefinirSplit(percentual);
        _repo.Atualizar(pagamento);
        await _repo.SalvarAsync();
        return MapearParaDto(pagamento);
    }

    private static PagamentoDto MapearParaDto(Pagamento p) => new()
    {
        Id = p.Id, TutorId = p.TutorId, ConsultaId = p.ConsultaId, InternacaoId = p.InternacaoId,
        Valor = p.Valor, MeioPagamento = p.MeioPagamento, Momento = p.Momento,
        StatusPagamento = p.StatusPagamento, PercentualSplit = p.PercentualSplit,
        ValorEstornado = p.ValorEstornado
    };
}
