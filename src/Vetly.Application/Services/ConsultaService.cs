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
/// Servico de consultas. Orquestra agendamento (RN-006) e cancelamento via Strategy (RN-014/RN-041/RN-042).
/// </summary>
public class ConsultaService : IConsultaService
{
    private readonly IConsultaRepository _repo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IDocumentoRepository _documentoRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IVeterinarioRepository _veterinarioRepo;
    private readonly IEmpresaRepository _empresaRepo;
    private readonly IEnumerable<ICancelamentoStrategy> _strategies;

    public ConsultaService(
        IConsultaRepository repo,
        IPagamentoRepository pagamentoRepo,
        IDocumentoRepository documentoRepo,
        IAnimalRepository animalRepo,
        IVeterinarioRepository veterinarioRepo,
        IEmpresaRepository empresaRepo,
        IEnumerable<ICancelamentoStrategy> strategies)
    {
        _repo = repo;
        _pagamentoRepo = pagamentoRepo;
        _documentoRepo = documentoRepo;
        _animalRepo = animalRepo;
        _veterinarioRepo = veterinarioRepo;
        _empresaRepo = empresaRepo;
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
    /// RN-006: o agendamento so e confirmado se o pagamento associado estiver com status Confirmado.
    /// </summary>
    public async Task<ConsultaDto> AgendarAsync(CriarConsultaDto dto)
    {
        GarantirModalidadePresencial(dto.Modalidade);

        var pagamento = await _pagamentoRepo.ObterPorIdAsync(dto.PagamentoId)
            ?? throw new NotFoundException("Pagamento", dto.PagamentoId);

        if (pagamento.StatusPagamento != StatusPagamento.Confirmado)
            throw new BusinessRuleException("RN-006",
                "A consulta so pode ser agendada apos confirmacao do pagamento.");

        var consulta = new Consulta(dto.DataHora, dto.Modalidade, dto.VeterinarioId, dto.AnimalId, dto.TutorId);
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
        GarantirModalidadePresencial(dto.Modalidade);

        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);
        consulta.Reagendar(dto.DataHora);
        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();
    }

    /// <summary>
    /// Cancela a consulta aplicando a Strategy de reembolso de menor prioridade aplicavel (RN-014/RN-041/RN-042).
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

        // RN-042: a politica de retencao e da clinica, nao da plataforma
        var percentualRetencao = await ObterPercentualRetencaoAsync(consulta.VeterinarioId);
        var resultado = strategy.Executar(pagamento, percentualRetencao);

        pagamento.Estornar(resultado.ValorReembolso);
        consulta.Cancelar();

        _repo.Atualizar(consulta);
        _pagamentoRepo.Atualizar(pagamento);
        await _repo.SalvarAsync();

        return resultado;
    }

    /// <summary>
    /// Finaliza a consulta exigindo receita veterinaria assinada digitalmente (RN-087).
    /// </summary>
    public async Task FinalizarAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        var receita = await _documentoRepo.ObterPorConsultaETipoAsync(consultaId, TipoDocumento.ReceitaVeterinaria);
        if (receita is null)
            throw new BusinessRuleException("RN-087", "Receita veterinaria nao encontrada para esta consulta.");
        if (!receita.AssinadoDigitalmente)
            throw new BusinessRuleException("RN-087", "A receita veterinaria deve estar assinada digitalmente.");

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
                LiberadoAoTutor = e.LiberadoAoTutor,
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
                IdadeEmAnos = animal.IdadeEmAnos(), TutorId = animal.TutorId,
                AlertasAtivos = animal.AlertasAtivos, Ativo = animal.Ativo
            },
            HistoricoResumido = historico,
            AlertasAtivos = animal.AlertasAtivos,
            ExamesRecentes = exames,
            UltimaConsulta = historico.Count > 0 ? historico[0].DataHora : null
        };
    }

    /// <summary>
    /// Registra a validacao manual do diagnostico pelo veterinario (RN-082).
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

    /// <summary>
    /// Percentual de retencao aplicavel ao cancelamento parcial (RN-042).
    ///
    /// A politica pertence a clinica e e configurada no onboarding: le-se de
    /// <c>Empresa.PercentualRetencaoParcial</c> pelo vinculo do veterinario.
    ///
    /// Veterinario autonomo nao tem empresa e ainda nao tem coluna propria de politica
    /// (entra junto de PUT /api/veterinarios/{id}/servicos, na onda 3); ate la vale o
    /// padrao de 30% do onboarding, que e o mesmo valor que estava fixo no codigo.
    /// </summary>
    private async Task<decimal> ObterPercentualRetencaoAsync(Guid veterinarioId)
    {
        var vet = await _veterinarioRepo.ObterPorIdAsync(veterinarioId);
        if (vet?.EmpresaId is null)
            return Empresa.PercentualRetencaoPadrao;

        var empresa = await _empresaRepo.ObterPorIdAsync(vet.EmpresaId.Value);
        return empresa?.PercentualRetencaoParcial ?? Empresa.PercentualRetencaoPadrao;
    }

    /// <summary>
    /// RN-039: no MVP todo atendimento e presencial. O valor <c>Remoto</c> permanece no enum
    /// (remove-lo exigiria migration por nada), mas e rejeitado na entrada com mensagem clara.
    /// </summary>
    private static void GarantirModalidadePresencial(ModalidadeAtendimento modalidade)
    {
        if (modalidade == ModalidadeAtendimento.Remoto)
            throw new BusinessRuleException("RN-039",
                "Atendimento remoto esta fora do escopo desta fase. Agende como Presencial.");
    }

    private static ConsultaDto MapearParaDto(Consulta c) => new()
    {
        Id = c.Id, DataHora = c.DataHora, Modalidade = c.Modalidade,
        VeterinarioId = c.VeterinarioId, AnimalId = c.AnimalId, TutorId = c.TutorId,
        DiagnosticoValidado = c.DiagnosticoValidado, ProtocoloValidado = c.ProtocoloValidado,
        StatusPagamento = c.StatusPagamento, Cancelada = c.Cancelada, Finalizada = c.Finalizada
    };
}
