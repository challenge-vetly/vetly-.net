using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Split;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>Servico de pagamentos. Processa criacao e split financeiro via Strategy.</summary>
public class PagamentoService : IPagamentoService
{
    private readonly IPagamentoRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IEnumerable<ISplitFinanceiroStrategy> _splitStrategies;
    private readonly IUsuarioAtual _usuario;

    public PagamentoService(
        IPagamentoRepository repo,
        IVeterinarioRepository vetRepo,
        IConsultaRepository consultaRepo,
        IEnumerable<ISplitFinanceiroStrategy> splitStrategies,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _consultaRepo = consultaRepo;
        _splitStrategies = splitStrategies;
        _usuario = usuario;
    }

    /// <summary>
    /// Lista pagamentos no escopo de quem chama: o Responsável vê a própria carteira,
    /// o Admin vê o consolidado (RN-106).
    /// </summary>
    public async Task<ResultadoPaginado<PagamentoDto>> ObterTodosAsync(Paginacao paginacao)
    {
        // Escopo do token, nunca parametro do cliente. Sem escopo reconhecido, nada.
        Guid? tutorId = _usuario.EhAdmin ? null : (_usuario.TutorId ?? Guid.Empty);

        var pagina = await _repo.ObterPaginadoAsync(paginacao, tutorId);
        return pagina.Mapear(MapearParaDto);
    }

    public async Task<PagamentoDto> ObterPorIdAsync(Guid id)
    {
        var pagamento = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Pagamento", id);

        if (!_usuario.EhAdmin && _usuario.TutorId != pagamento.TutorId)
            throw new AcessoNegadoException("RN-106", "Este pagamento nao pertence ao seu escopo de acesso.");

        return MapearParaDto(pagamento);
    }

    public async Task<PagamentoDto> CriarAsync(CriarPagamentoDto dto)
    {
        var pagamento = new Pagamento(dto.TutorId, dto.Valor, dto.MeioPagamento, dto.ConsultaId, dto.InternacaoId);
        pagamento.Confirmar(); // pagamento confirmado ao criar (integracao com gateway real seria assincrona)
        await _repo.AdicionarAsync(pagamento);
        await _repo.SalvarAsync();
        return MapearParaDto(pagamento);
    }

    /// <summary>
    /// Processa o split financeiro aplicando a Strategy correta com base na persona do veterinario.
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
