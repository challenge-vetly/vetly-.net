using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Cancelamento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Application.Interfaces;

namespace Vetly.Application.Services;

/// <summary>
/// ServiÃ§o de consultas. Orquestra agendamento (RN-015) e cancelamento via Strategy (RN-019/020/021).
/// </summary>
public class ConsultaService : IConsultaService
{
    private readonly IConsultaRepository _repo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IEnumerable<ICancelamentoStrategy> _strategies;

    public ConsultaService(
        IConsultaRepository repo,
        IPagamentoRepository pagamentoRepo,
        IEnumerable<ICancelamentoStrategy> strategies)
    {
        _repo = repo;
        _pagamentoRepo = pagamentoRepo;
        _strategies = strategies;
    }

    public async Task<IEnumerable<ConsultaDto>> ObterTodosAsync(
        DateTime? dataInicio, DateTime? dataFim, Guid? veterinarioId, bool? cancelada)
    {
        var consultas = await _repo.ObterComFiltrosAsync(dataInicio, dataFim, veterinarioId, cancelada);
        return consultas.Select(MapearParaDto);
    }

    public async Task<ConsultaDto> ObterPorIdAsync(Guid id)
    {
        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);
        return MapearParaDto(consulta);
    }

    public async Task<IEnumerable<ConsultaDto>> ObterPorVeterinarioAsync(Guid veterinarioId)
    {
        var consultas = await _repo.ObterPorVeterinarioAsync(veterinarioId);
        return consultas.Select(MapearParaDto);
    }

    public async Task<IEnumerable<ConsultaDto>> ObterPorAnimalAsync(Guid animalId)
    {
        var consultas = await _repo.ObterPorAnimalAsync(animalId);
        return consultas.Select(MapearParaDto);
    }

    /// <summary>
    /// RN-015: o agendamento sÃ³ Ã© confirmado se o pagamento associado estiver com status Confirmado.
    /// </summary>
    public async Task<ConsultaDto> AgendarAsync(CriarConsultaDto dto)
    {
        var pagamento = await _pagamentoRepo.ObterPorIdAsync(dto.PagamentoId)
            ?? throw new NotFoundException("Pagamento", dto.PagamentoId);

        if (pagamento.StatusPagamento != StatusPagamento.Confirmado)
            throw new BusinessRuleException("RN-015",
                "A consulta sÃ³ pode ser agendada apÃ³s confirmaÃ§Ã£o do pagamento.");

        var consulta = new Consulta(dto.DataHora, dto.Modalidade, dto.VeterinarioId, dto.AnimalId, dto.TutorId);
        consulta.ConfirmarPagamento();

        await _repo.AdicionarAsync(consulta);
        await _repo.SalvarAsync();
        return MapearParaDto(consulta);
    }

    public async Task AtualizarAsync(Guid id, CriarConsultaDto dto)
    {
        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);
        consulta.Reagendar(dto.DataHora);
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();
    }

    /// <summary>
    /// Cancela a consulta aplicando a Strategy de reembolso de menor prioridade aplicÃ¡vel (RN-019/020/021).
    /// </summary>
    public async Task<ResultadoCancelamentoDto> CancelarAsync(Guid id)
    {
        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);

        if (consulta.Cancelada)
            throw new BusinessRuleException("CONSULTA-001", "Esta consulta jÃ¡ foi cancelada.");

        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(id)
            ?? throw new BusinessRuleException("CONSULTA-002", "Pagamento da consulta nÃ£o encontrado.");

        // Seleciona a strategy de menor prioridade que seja aplicÃ¡vel ao momento do cancelamento
        var strategy = _strategies
            .OrderBy(s => s.Prioridade)
            .First(s => s.Aplicavel(consulta.DataHora, DateTime.UtcNow));

        var resultado = strategy.Executar(pagamento, percentualRetencao: 30m);

        pagamento.Estornar(resultado.ValorReembolso);
        consulta.Cancelar();

        _repo.Atualizar(consulta);
        _pagamentoRepo.Atualizar(pagamento);
        await _repo.SalvarAsync();

        return resultado;
    }

    private static ConsultaDto MapearParaDto(Consulta c) => new()
    {
        Id = c.Id, DataHora = c.DataHora, Modalidade = c.Modalidade,
        VeterinarioId = c.VeterinarioId, AnimalId = c.AnimalId, TutorId = c.TutorId,
        DiagnosticoValidado = c.DiagnosticoValidado, ProtocoloValidado = c.ProtocoloValidado,
        StatusPagamento = c.StatusPagamento, Cancelada = c.Cancelada
    };
}
