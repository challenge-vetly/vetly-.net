using System.Text.Json;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Comum;
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
    private readonly IUsuarioAtual _usuario;
    private readonly IAgendaRepository _agendaRepo;
    private readonly IFilaDeJobs _fila;
    private readonly IFidelidadeService _fidelidade;

    public ConsultaService(
        IConsultaRepository repo,
        IPagamentoRepository pagamentoRepo,
        IDocumentoRepository documentoRepo,
        IAnimalRepository animalRepo,
        IVeterinarioRepository veterinarioRepo,
        IEmpresaRepository empresaRepo,
        IEnumerable<ICancelamentoStrategy> strategies,
        IUsuarioAtual usuario,
        IAgendaRepository agendaRepo,
        IFilaDeJobs fila,
        IFidelidadeService fidelidade)
    {
        _repo = repo;
        _pagamentoRepo = pagamentoRepo;
        _documentoRepo = documentoRepo;
        _animalRepo = animalRepo;
        _veterinarioRepo = veterinarioRepo;
        _empresaRepo = empresaRepo;
        _strategies = strategies;
        _usuario = usuario;
        _agendaRepo = agendaRepo;
        _fila = fila;
        _fidelidade = fidelidade;
    }

    /// <summary>
    /// Trava o horario e cria a consulta em EmCheckout (RN-035).
    ///
    /// Esta e a trilha do app, e convive com POST /api/consultas, que continua sendo o
    /// caminho da emergencia presencial e do balcao (RN-040) — resolucao do conflito
    /// C-02 sem refatorar a rota existente.
    /// </summary>
    public async Task<CheckoutCriadoDto> IniciarCheckoutAsync(CheckoutDto dto)
    {
        var animal = await _animalRepo.ObterPorIdAsync(dto.AnimalId)
            ?? throw new NotFoundException("Animal", dto.AnimalId);

        if (_usuario.EhTutor && _usuario.TutorId != animal.TutorId)
            throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");

        var slot = await _agendaRepo.ObterSlotAsync(dto.SlotId)
            ?? throw new NotFoundException("Horario", dto.SlotId);

        var servico = await _agendaRepo.ObterServicoAsync(dto.ServicoId)
            ?? throw new NotFoundException("Servico", dto.ServicoId);

        if (!servico.Ativo)
            throw new BusinessRuleException("RN-032", "Este servico nao esta mais disponivel.");

        if (servico.PrestadorId != dto.PrestadorId)
            throw new BusinessRuleException("RN-032", "O servico informado nao pertence a este prestador.");

        var vet = await _veterinarioRepo.ObterPorIdAsync(slot.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", slot.VeterinarioId);

        // RN-003: com clinica, quem atende e o profissional que ela designa — aqui, o dono
        // do horario escolhido. So e preciso conferir que ele e mesmo da unidade.
        Guid? empresaId = null;
        if (dto.PrestadorId != vet.Id)
        {
            if (vet.EmpresaId != dto.PrestadorId)
                throw new BusinessRuleException("RN-003", "O horario escolhido nao pertence a este prestador.");

            empresaId = dto.PrestadorId;
        }

        if (slot.Inicio <= DateTime.UtcNow)
            throw new BusinessRuleException("RN-034", "Este horario ja passou.");

        var consulta = Consulta.ParaCheckout(
            slot.Inicio, vet.Id, animal.Id, animal.TutorId, slot.Id, servico.Id, empresaId);

        // Um unico ponto decide quem fica com o horario. Quem chega depois recebe 409 e
        // escolhe outro — e o que impede overbooking sem gateway real (RN-035).
        if (!slot.TravarParaCheckout(consulta.Id, DateTime.UtcNow))
            throw new ConflitoDeEstadoException("RN-035", "Este horario acabou de ser reservado por outra pessoa.");

        _agendaRepo.AtualizarSlot(slot);
        await _agendaRepo.SalvarAsync();

        await _repo.AdicionarAsync(consulta);
        await _repo.SalvarAsync();

        return new CheckoutCriadoDto
        {
            ConsultaId = consulta.Id,
            Status = consulta.Status,
            LockExpiraEm = slot.LockAte!.Value,
            Resumo = new ResumoDoCheckoutDto
            {
                Prestador = empresaId is null ? vet.Nome : await NomeDaEmpresaAsync(empresaId.Value, vet.Nome),
                Servico = servico.Tipo,
                DataHora = slot.Inicio,
                Valor = servico.Valor,
                Modalidade = consulta.Modalidade,
                PoliticaReembolso = new PoliticaDeReembolsoDto
                {
                    PercentualRetencaoParcial = await ObterPercentualRetencaoAsync(vet.Id)
                }
            }
        };
    }

    private async Task<string> NomeDaEmpresaAsync(Guid empresaId, string nomeDoVet)
    {
        var empresa = await _empresaRepo.ObterPorIdAsync(empresaId);
        return empresa is null ? nomeDoVet : empresa.Nome;
    }

    public async Task<ResultadoPaginado<ConsultaDto>> ObterTodosAsync(
        FiltroConsultaDto filtro, Paginacao paginacao)
    {
        var pagina = await _repo.ObterComFiltrosAsync(AplicarEscopo(filtro), paginacao);
        return pagina.Mapear(MapearParaDto);
    }

    /// <summary>
    /// Fixa no filtro o escopo de quem chama (RN-105/RN-106). O valor vem do token, e
    /// sobrescreve o que veio na query string: senao bastaria trocar o tutorId da URL
    /// para ler a agenda de outra pessoa.
    /// </summary>
    private FiltroConsultaDto AplicarEscopo(FiltroConsultaDto filtro)
    {
        if (_usuario.EhAdmin)
            return filtro;

        if (_usuario.EhTutor && _usuario.TutorId is { } tutorId)
        {
            filtro.TutorId = tutorId;
            return filtro;
        }

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId)
        {
            filtro.VeterinarioId = vetId;
            return filtro;
        }

        // Sem escopo reconhecido nao ha o que listar — falha fechado
        filtro.TutorId = Guid.Empty;
        return filtro;
    }

    /// <summary>Recusa acesso a consulta fora do escopo de quem chama (RN-105/RN-106).</summary>
    private void GarantirAcessoAConsulta(Consulta consulta)
    {
        if (_usuario.EhAdmin)
            return;

        if (_usuario.EhTutor && _usuario.TutorId == consulta.TutorId)
            return;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId == consulta.VeterinarioId)
            return;

        throw new AcessoNegadoException("RN-105", "Esta consulta nao pertence ao seu escopo de acesso.");
    }

    public async Task<ConsultaDto> ObterPorIdAsync(Guid id)
    {
        var consulta = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Consulta", id);

        GarantirAcessoAConsulta(consulta);
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

        // RN-052: pontos so valem para consulta confirmada E realizada. Cancelada,
        // o credito e desfeito — senao o programa teria pago por um atendimento que
        // nao aconteceu.
        await _fidelidade.EstornarPorConsultaAsync(consulta.Id);

        // O horario volta a valer, e quem esta na fila de espera precisa saber (RN-037)
        await LiberarHorarioAsync(consulta);

        return resultado;
    }

    /// <inheritdoc/>
    public async Task<SimulacaoDeCancelamentoDto> SimularCancelamentoAsync(Guid consultaId)
    {
        var consulta = await ObterNoEscopoAsync(consultaId);

        if (consulta.Cancelada)
            throw new BusinessRuleException("CONSULTA-001", "Esta consulta ja foi cancelada.");

        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(consultaId)
            ?? throw new BusinessRuleException("CONSULTA-002", "Pagamento da consulta nao encontrado.");

        var agora = DateTime.UtcNow;

        // A MESMA selecao de strategy do cancelamento, de proposito: se a simulacao
        // usasse outro criterio, ela mostraria um valor e o cancelamento cobraria
        // outro — que e exatamente o que a RN-042 quer evitar ao exigir transparencia.
        var strategy = _strategies
            .OrderBy(s => s.Prioridade)
            .First(s => s.Aplicavel(consulta.DataHora, agora));

        var percentualRetencao = await ObterPercentualRetencaoAsync(consulta.VeterinarioId);

        // O Strategy apenas calcula; quem muda o estado do pagamento e o cancelamento.
        // Simular nao pode deixar rastro.
        var resultado = strategy.Executar(pagamento, percentualRetencao);

        return new SimulacaoDeCancelamentoDto
        {
            ConsultaId = consultaId,
            EstrategiaAplicada = resultado.EstrategiaAplicada,
            HorasDeAntecedencia = Math.Round((consulta.DataHora - agora).TotalHours, 1),
            ValorPago = pagamento.Valor,
            PercentualRetencao = resultado.PercentualRetencao,
            ValorRetido = pagamento.Valor - resultado.ValorReembolso,
            ValorReembolso = resultado.ValorReembolso,
            Liquidacao = "Simulada"
        };
    }

    /// <inheritdoc/>
    public async Task RegistrarPreSintomasAsync(Guid consultaId, PreSintomasDto dto)
    {
        var consulta = await ObterNoEscopoAsync(consultaId);

        // Pre-sintoma depois do atendimento nao alimenta nada: o briefing ja foi lido
        // e a IA ja recebeu o contexto (RN-005/RN-078).
        if (consulta.Status is not (StatusConsulta.EmCheckout or StatusConsulta.Confirmada))
            throw new BusinessRuleException("RN-036",
                "Os pre-sintomas so podem ser informados antes do atendimento.");

        consulta.RegistrarPreSintomas(JsonSerializer.Serialize(dto, OpcoesDeJson), dto.MidiaIds);

        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<RemarcacaoRealizadaDto> RemarcarAsync(Guid consultaId, RemarcarConsultaDto dto)
    {
        var consulta = await ObterNoEscopoAsync(consultaId);

        if (consulta.ContadorRemarcacoes >= Consulta.LimiteDeRemarcacoes)
            throw new BusinessRuleException("RN-043",
                $"Esta consulta ja atingiu o limite de {Consulta.LimiteDeRemarcacoes} remarcacoes. " +
                "Resta cancelar sob a politica vigente.");

        var novoSlot = await _agendaRepo.ObterSlotAsync(dto.NovoSlotId)
            ?? throw new NotFoundException("Slot", dto.NovoSlotId);

        if (novoSlot.VeterinarioId != consulta.VeterinarioId)
            throw new ValidationException("novoSlotId",
                "A remarcacao mantem o mesmo veterinario. Para trocar de profissional, cancele e agende de novo.");

        // Trava antes de mover: sem isso, duas remarcacoes simultaneas mandariam dois
        // animais para o mesmo horario (RN-035).
        if (!novoSlot.TravarParaCheckout(consulta.Id, DateTime.UtcNow))
            throw new ConflitoDeEstadoException("RN-035",
                "O horario escolhido nao esta mais disponivel.");

        var horarioAnterior = consulta.DataHora;
        var slotAnterior = consulta.SlotId;

        consulta.RemarcarPara(novoSlot.Inicio, novoSlot.Id);

        // RN-013: o pagamento ja realizado e transferido, sem nova cobranca
        if (consulta.StatusPagamento == StatusPagamento.Confirmado)
            novoSlot.Confirmar();

        _agendaRepo.AtualizarSlot(novoSlot);
        _repo.Atualizar(consulta);

        await _agendaRepo.SalvarAsync();
        await _repo.SalvarAsync();

        // O horario antigo volta a fila: e vaga que alguem esta esperando (RN-037)
        await LiberarSlotAsync(slotAnterior);

        return new RemarcacaoRealizadaDto
        {
            ConsultaId = consulta.Id,
            HorarioAnterior = horarioAnterior,
            NovoHorario = consulta.DataHora,
            Remarcacoes = consulta.ContadorRemarcacoes,
            RemarcacoesRestantes = consulta.RemarcacoesRestantes(),
            StatusPagamento = consulta.StatusPagamento
        };
    }

    /// <inheritdoc/>
    public async Task<NoShowRegistradoDto> RegistrarNoShowAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        // Quem registra o nao comparecimento e quem estava esperando: o profissional
        // ou a unidade. O Responsavel nao declara o proprio no-show.
        if (!_usuario.EhAdmin && _usuario.VeterinarioId != consulta.VeterinarioId)
            throw new AcessoNegadoException("RN-105", "Esta consulta nao pertence ao seu escopo de acesso.");

        if (consulta.Status != StatusConsulta.Confirmada)
            throw new BusinessRuleException("RN-044",
                $"Consulta com status {consulta.Status} nao pode ser marcada como no-show.");

        consulta.RegistrarNoShow();

        _repo.Atualizar(consulta);
        await _repo.SalvarAsync();

        // RN-044: sem reembolso, seguindo a faixa "menos de 2h ou no ato" da RN-014.
        // Nao ha penalidade nova — reaproveita a politica que ja existia.
        return new NoShowRegistradoDto
        {
            ConsultaId = consulta.Id,
            Status = consulta.Status,
            GerouReembolso = false,
            RegistradoEm = DateTime.UtcNow
        };
    }

    /// <summary>Devolve um horario a disponibilidade e avisa a lista de espera (RN-037).</summary>
    private async Task LiberarSlotAsync(Guid? slotId)
    {
        if (slotId is not { } id)
            return;

        var slot = await _agendaRepo.ObterSlotAsync(id);

        if (slot is null)
            return;

        slot.Liberar();
        _agendaRepo.AtualizarSlot(slot);
        await _agendaRepo.SalvarAsync();

        await _fila.EnfileirarAsync(TipoJob.PromoverListaEspera, slot.Id.ToString());
    }

    /// <summary>
    /// A consulta e do Responsavel que a agendou; o veterinario que a conduz e o Admin
    /// tambem alcancam (RN-105/RN-106).
    /// </summary>
    private async Task<Consulta> ObterNoEscopoAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (_usuario.EhAdmin
            || _usuario.TutorId == consulta.TutorId
            || _usuario.VeterinarioId == consulta.VeterinarioId)
        {
            return consulta;
        }

        throw new AcessoNegadoException("RN-105", "Esta consulta nao pertence ao seu escopo de acesso.");
    }

    /// <summary>Serializacao dos pre-sintomas: camelCase, como o resto do contrato.</summary>
    private static readonly JsonSerializerOptions OpcoesDeJson = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Finaliza a consulta exigindo receita veterinaria assinada digitalmente (RN-087).
    /// </summary>
    public async Task FinalizarAsync(Guid consultaId)
    {
        var consulta = await _repo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        // C-04: a exigencia da RN-087 e sobre o documento que existe, nao sobre a
        // consulta. Consulta de rotina, vacinacao ou retorno frequentemente nao
        // prescrevem nada, e exigir receita em todas travaria o atendimento correto —
        // o efeito seria o veterinario emitir receita vazia so para conseguir fechar
        // a consulta, que e o oposto do que a regra protege.
        //
        // O que passa a valer: todo documento ja emitido que exige assinatura precisa
        // estar assinado. Sem receita nem atestado, nada bloqueia.
        var documentos = await _documentoRepo.ObterPorConsultaAsync(consultaId);

        var pendente = documentos.FirstOrDefault(d => d.PendenteDeAssinatura());

        if (pendente is not null)
            throw new BusinessRuleException("RN-087",
                $"O documento do tipo {pendente.TipoDocumento} precisa estar assinado digitalmente " +
                "antes de finalizar a consulta.");

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
    /// Devolve o horario da consulta a disponibilidade e avisa a lista de espera.
    /// Toda entrada em "livre" dispara a promocao do primeiro da fila (RN-037).
    /// </summary>
    private async Task LiberarHorarioAsync(Consulta consulta)
    {
        if (consulta.SlotId is not { } slotId)
            return;

        var slot = await _agendaRepo.ObterSlotAsync(slotId);
        if (slot is null) return;

        slot.Liberar();
        _agendaRepo.AtualizarSlot(slot);
        await _agendaRepo.SalvarAsync();

        await _fila.EnfileirarAsync(TipoJob.PromoverListaEspera, slot.Id.ToString());
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
        StatusPagamento = c.StatusPagamento, Status = c.Status,
        Cancelada = c.Cancelada, Finalizada = c.Finalizada
    };
}
