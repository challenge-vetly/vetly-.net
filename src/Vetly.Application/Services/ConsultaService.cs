using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Exame;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Cancelamento;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de consultas. Orquestra agendamento (RN-015) e cancelamento via Strategy (RN-019/020/021).
/// </summary>
public class ConsultaService : IConsultaService
{
    private readonly IConsultaRepository _repo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IDocumentoRepository _documentoRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IConsentimentoLgpdRepository _consentimentoRepo;
    private readonly IEnumerable<ICancelamentoStrategy> _strategies;

    public ConsultaService(
        IConsultaRepository repo,
        IPagamentoRepository pagamentoRepo,
        IDocumentoRepository documentoRepo,
        IAnimalRepository animalRepo,
        IConsentimentoLgpdRepository consentimentoRepo,
        IEnumerable<ICancelamentoStrategy> strategies)
    {
        _repo = repo;
        _pagamentoRepo = pagamentoRepo;
        _documentoRepo = documentoRepo;
        _animalRepo = animalRepo;
        _consentimentoRepo = consentimentoRepo;
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
    /// RN-015: o agendamento so e confirmado se o pagamento associado estiver com status Confirmado.
    /// </summary>
    public async Task<ConsultaDto> AgendarAsync(CriarConsultaDto dto)
    {
        var temConsentimentoClinico = await _consentimentoRepo.ObterAtivoAsync(
            dto.ResponsavelId, FinalidadeConsentimento.AtendimentoClinico) is not null;
        if (!temConsentimentoClinico)
            throw new BusinessRuleException("LGPD-001",
                "O responsavel precisa ter o consentimento de atendimento clinico ativo para agendar consultas.");

        var pagamento = await _pagamentoRepo.ObterPorIdAsync(dto.PagamentoId)
            ?? throw new NotFoundException("Pagamento", dto.PagamentoId);

        if (pagamento.StatusPagamento != StatusPagamento.Confirmado)
            throw new BusinessRuleException("RN-015",
                "A consulta so pode ser agendada apos confirmacao do pagamento.");

        var consulta = new Consulta(dto.DataHora, dto.Modalidade, dto.VeterinarioId, dto.AnimalId, dto.ResponsavelId);
        consulta.ConfirmarPagamento();

        await _repo.AdicionarAsync(consulta);
        await _repo.SalvarAsync();

        // Fecha o vinculo bidirecional Pagamento→Consulta; sem isso CancelarAsync lanca CONSULTA-002.
        pagamento.VincularConsulta(consulta.Id);
        _pagamentoRepo.Atualizar(pagamento);
        await _pagamentoRepo.SalvarAsync();

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
    /// Cancela a consulta aplicando a Strategy de reembolso de menor prioridade aplicavel (RN-019/020/021).
    /// </summary>
    public async Task<ResultadoCancelamentoDto> CancelarAsync(Guid id)
    {
        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);

        if (consulta.Cancelada)
            throw new BusinessRuleException("CONSULTA-001", "Esta consulta ja foi cancelada.");

        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(id)
            ?? throw new BusinessRuleException("CONSULTA-002", "Pagamento da consulta nao encontrado.");

        // Seleciona a strategy de menor prioridade que seja aplicavel ao momento do cancelamento
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

    /// <summary>
    /// Finaliza a consulta exigindo receita veterinaria assinada digitalmente (RN-031).
    /// </summary>
    public async Task FinalizarAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        var receita = await _documentoRepo.ObterPorConsultaETipoAsync(consultaId, TipoDocumento.ReceitaVeterinaria);
        if (receita is null)
            throw new BusinessRuleException("RN-031", "Receita veterinaria nao encontrada para esta consulta.");
        if (!receita.AssinadoDigitalmente)
            throw new BusinessRuleException("RN-031", "A receita veterinaria deve estar assinada digitalmente.");

        consulta.Finalizar();
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();
    }

    /// <summary>
    /// Agrega dados pre-consulta: animal, historico (ultimas 5), exames recentes (ultimos 3).
    /// </summary>
    public async Task<BriefingConsultaDto> ObterBriefingAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId)
            ?? throw new NotFoundException("Animal", consulta.AnimalId);

        var historico = (await _repo.ObterPorAnimalAsync(consulta.AnimalId))
            .OrderByDescending(c => c.DataHora)
            .Take(5)
            .Select(MapearParaDto)
            .ToList();

        var exames = (await _animalRepo.ObterExamesAsync(consulta.AnimalId))
            .OrderByDescending(e => e.DataSolicitacao)
            .Take(3)
            .Select(e => new ExameDto
            {
                Id = e.Id, AnimalId = e.AnimalId, VeterinarioId = e.VeterinarioId,
                TipoSolicitacao = e.TipoSolicitacao, Resultado = e.Resultado,
                LiberadoAoResponsavel = e.LiberadoAoResponsavel,
                DataSolicitacao = e.DataSolicitacao, DataResultado = e.DataResultado
            })
            .ToList();

        return new BriefingConsultaDto
        {
            ConsultaId = consultaId,
            Animal = new AnimalDto
            {
                Id = animal.Id, Nome = animal.Nome, Especie = animal.Especie,
                Raca = animal.Raca, DataNascimento = animal.DataNascimento,
                IdadeEmAnos = animal.IdadeEmAnos(), ResponsavelId = animal.ResponsavelId,
                AlertasAtivos = animal.AlertasAtivos, Ativo = animal.Ativo
            },
            HistoricoResumido = historico,
            AlertasAtivos = animal.AlertasAtivos,
            ExamesRecentes = exames,
            UltimaConsulta = historico.Count > 0 ? historico[0].DataHora : null
        };
    }

    /// <summary>
    /// Registra a validacao manual do diagnostico pelo veterinario (RN-024).
    /// Pre-requisito para gerar documentos via DocumentoService.
    /// </summary>
    public async Task ValidarDiagnosticoAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (consulta.Cancelada)
            throw new BusinessRuleException("CONSULTA-003",
                "Nao e possivel validar diagnostico de consulta cancelada.");

        consulta.ValidarDiagnostico();
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();
    }

    private static ConsultaDto MapearParaDto(Consulta c) => new()
    {
        Id = c.Id, DataHora = c.DataHora, Modalidade = c.Modalidade,
        VeterinarioId = c.VeterinarioId, AnimalId = c.AnimalId, ResponsavelId = c.ResponsavelId,
        DiagnosticoValidado = c.DiagnosticoValidado, ProtocoloValidado = c.ProtocoloValidado,
        StatusPagamento = c.StatusPagamento, Cancelada = c.Cancelada, Finalizada = c.Finalizada
    };
}
