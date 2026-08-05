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
/// Servico de consultas. Orquestra a máquina de estados do agendamento (RN-056..061),
/// pré-sintomas (RN-059), consentimento LGPD (RN-041/084) e cancelamento via Strategy
/// (RN-019/020/021).
/// </summary>
public class ConsultaService : IConsultaService
{
    // Procedimentos físicos exigem modalidade presencial — reafirma RN-025/057.
    private static readonly HashSet<TipoServico> ServicosFisicos =
        [TipoServico.Vacinacao, TipoServico.Cirurgia, TipoServico.Exame];

    private readonly IConsultaRepository _repo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IDocumentoRepository _documentoRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IConsentimentoLgpdRepository _consentimentoRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly IEnumerable<ICancelamentoStrategy> _strategies;

    public ConsultaService(
        IConsultaRepository repo,
        IPagamentoRepository pagamentoRepo,
        IDocumentoRepository documentoRepo,
        IAnimalRepository animalRepo,
        IConsentimentoLgpdRepository consentimentoRepo,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        IEnumerable<ICancelamentoStrategy> strategies)
    {
        _repo = repo;
        _pagamentoRepo = pagamentoRepo;
        _documentoRepo = documentoRepo;
        _animalRepo = animalRepo;
        _consentimentoRepo = consentimentoRepo;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _strategies = strategies;
    }

    public async Task<IEnumerable<ConsultaDto>> ObterTodosAsync(
        DateTime? dataInicio, DateTime? dataFim, Guid? veterinarioId, StatusConsulta? status)
    {
        var consultas = await _repo.ObterComFiltrosAsync(dataInicio, dataFim, veterinarioId, status);
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
    /// Agenda uma consulta: exige consentimento de atendimento clínico ativo (LGPD-001),
    /// valida que serviços físicos sejam presenciais (RN-057) e cria a consulta já em
    /// EmCheckout com lock de 10 min (RN-058). O pagamento é confirmado depois, em uma
    /// etapa separada (RN-037).
    /// </summary>
    public async Task<ConsultaDto> AgendarAsync(CriarConsultaDto dto)
    {
        var temConsentimentoClinico = await _consentimentoRepo.ObterAtivoAsync(
            dto.ResponsavelId, FinalidadeConsentimento.AtendimentoClinico) is not null;
        if (!temConsentimentoClinico)
            throw new BusinessRuleException("LGPD-001",
                "O responsavel precisa ter o consentimento de atendimento clinico ativo para agendar consultas.");

        if (ServicosFisicos.Contains(dto.TipoServico) && dto.Modalidade != ModalidadeAtendimento.Presencial)
            throw new BusinessRuleException("RN-057",
                $"O servico '{dto.TipoServico}' exige modalidade presencial.");

        var consulta = new Consulta(
            dto.DataHora, dto.Modalidade, dto.TipoServico, dto.VeterinarioId,
            dto.AnimalId, dto.ResponsavelId, dto.PreSintomas);
        consulta.IniciarCheckout(_timeProvider.GetUtcNow().UtcDateTime);

        await _repo.AdicionarAsync(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>Confirma o pagamento da consulta, transicionando EmCheckout → Confirmada (RN-058).</summary>
    public async Task<ConsultaDto> ConfirmarPagamentoAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        consulta.ConfirmarPagamento(_timeProvider.GetUtcNow().UtcDateTime);
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>
    /// Cancela a consulta aplicando a Strategy de reembolso de menor prioridade aplicavel (RN-019/020/021).
    /// A transição de estado em si (só permitida a partir de EmCheckout/Confirmada) é validada
    /// pelo próprio domínio — CONSULTA-010 em qualquer outra origem.
    /// </summary>
    public async Task<ResultadoCancelamentoDto> CancelarAsync(Guid id)
    {
        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);

        // Valida a transição de estado antes de qualquer trabalho com pagamento/strategy —
        // CONSULTA-010 imediato se a consulta já estiver num estado que não permite cancelar.
        consulta.Cancelar();

        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(id)
            ?? throw new BusinessRuleException("CONSULTA-002", "Pagamento da consulta nao encontrado.");

        // Seleciona a strategy de menor prioridade que seja aplicavel ao momento do cancelamento
        var strategy = _strategies
            .OrderBy(s => s.Prioridade)
            .First(s => s.Aplicavel(consulta.DataHora, DateTime.UtcNow));

        var resultado = strategy.Executar(pagamento, percentualRetencao: 30m);

        pagamento.Estornar(resultado.ValorReembolso);

        _repo.Atualizar(consulta);
        _pagamentoRepo.Atualizar(pagamento);
        await _repo.SalvarAsync();

        return resultado;
    }

    /// <summary>
    /// Marca a consulta como realizada (RN-061): exige receita assinada digitalmente (RN-031)
    /// e que o chamador seja o veterinário responsável pela consulta.
    /// </summary>
    public async Task<ConsultaDto> MarcarRealizadaAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (_currentUser.EntidadeId is { } vetId && vetId != consulta.VeterinarioId)
            throw new ForbiddenException("ACESSO-002",
                "Somente o veterinario responsavel pode marcar esta consulta como realizada.");

        var receita = await _documentoRepo.ObterPorConsultaETipoAsync(consultaId, TipoDocumento.ReceitaVeterinaria);
        if (receita is null)
            throw new BusinessRuleException("RN-031", "Receita veterinaria nao encontrada para esta consulta.");
        if (!receita.AssinadoDigitalmente)
            throw new BusinessRuleException("RN-031", "A receita veterinaria deve estar assinada digitalmente.");

        consulta.MarcarRealizada(_timeProvider.GetUtcNow().UtcDateTime);
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>Registra no-show de uma das partes (RN-064/066). Consequências (crédito/strike) ficam para a Fase 6.</summary>
    public async Task<ConsultaDto> RegistrarNoShowAsync(Guid consultaId, ParteNoShow parte)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (parte == ParteNoShow.Responsavel)
            consulta.RegistrarNoShowResponsavel();
        else
            consulta.RegistrarNoShowVeterinario();

        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>Remarca a consulta para uma nova data/hora, incrementando o contador de remarcações (RN-022).</summary>
    public async Task<ConsultaDto> RemarcarAsync(Guid consultaId, DateTime novaDataHora)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        consulta.Reagendar(novaDataHora);
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        return MapearParaDto(consulta);
    }

    /// <summary>
    /// Agrega dados pre-consulta: animal, pré-sintomas, historico (ultimas 5), exames recentes (ultimos 3).
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
            Animal = AnimalService.MapearParaDto(animal),
            PreSintomas = consulta.PreSintomas,
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

        if (consulta.Status == StatusConsulta.Cancelada)
            throw new BusinessRuleException("CONSULTA-003",
                "Nao e possivel validar diagnostico de consulta cancelada.");

        consulta.ValidarDiagnostico();
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();
    }

    private static ConsultaDto MapearParaDto(Consulta c) => new()
    {
        Id = c.Id, DataHora = c.DataHora, Modalidade = c.Modalidade, TipoServico = c.TipoServico,
        VeterinarioId = c.VeterinarioId, AnimalId = c.AnimalId, ResponsavelId = c.ResponsavelId,
        PreSintomas = c.PreSintomas, Status = c.Status, LockCheckoutExpiraEm = c.LockCheckoutExpiraEm,
        ContadorRemarcacoes = c.ContadorRemarcacoes, DataRealizada = c.DataRealizada,
        DiagnosticoValidado = c.DiagnosticoValidado, ProtocoloValidado = c.ProtocoloValidado
    };
}
