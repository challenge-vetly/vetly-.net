using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Split;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>Servico de pagamentos. Processa criacao e split financeiro via Strategy.</summary>
public class PagamentoService : IPagamentoService
{
    private readonly IPagamentoRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IEmpresaRepository _empresaRepo;
    private readonly IPagamentoAdapter _adaptador;
    private readonly IAgendaRepository _agendaRepo;
    private readonly IEnumerable<ISplitFinanceiroStrategy> _splitStrategies;
    private readonly IUsuarioAtual _usuario;

    public PagamentoService(
        IPagamentoRepository repo,
        IVeterinarioRepository vetRepo,
        IConsultaRepository consultaRepo,
        IEmpresaRepository empresaRepo,
        IPagamentoAdapter adaptador,
        IAgendaRepository agendaRepo,
        IEnumerable<ISplitFinanceiroStrategy> splitStrategies,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _consultaRepo = consultaRepo;
        _empresaRepo = empresaRepo;
        _adaptador = adaptador;
        _agendaRepo = agendaRepo;
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

    /// <summary>
    /// Cria a cobranca: apura o split na Vetly (RN-070), registra a cobranca no
    /// adaptador e devolve as instrucoes de pagamento.
    ///
    /// O pagamento NAO nasce confirmado. Quem confirma e o webhook, nunca a resposta
    /// sincrona — e o que mantem o fluxo pronto para um gateway real (vetly-tech §7.5).
    /// </summary>
    public async Task<CobrancaCriadaRespostaDto> CriarCobrancaAsync(CriarPagamentoDto dto)
    {
        var pagamento = new Pagamento(dto.TutorId, dto.Valor, dto.MeioPagamento, dto.ConsultaId, dto.InternacaoId);
        pagamento.DefinirTipo(dto.InternacaoId is null ? TipoPagamento.Consulta : TipoPagamento.Caucao);

        // O split e calculado pela Vetly, nunca pelo provedor (RN-051/RN-070)
        var split = await ApurarSplitAsync(pagamento);

        var chave = pagamento.Id.ToString();
        var cobranca = await _adaptador.CriarCobrancaAsync(
            new CriarCobrancaRequest(chave, pagamento.Id, pagamento.Valor, pagamento.MeioPagamento));

        pagamento.RegistrarCobranca(cobranca.ReferenciaExterna, chave);

        await _repo.AdicionarAsync(pagamento);
        await _repo.SalvarAsync();

        var partes = cobranca.Instrucoes.Split(SeparadorDaInstrucao);

        return new CobrancaCriadaRespostaDto
        {
            Id = pagamento.Id,
            StatusPagamento = pagamento.StatusPagamento,
            Valor = pagamento.Valor,
            Split = new SplitDto
            {
                Plano = split.Plano,
                TakeRate = split.TakeRate,
                ComissaoVetly = split.Comissao,
                Repasse = split.Repasse,
                DestinatarioRepasseId = pagamento.DestinatarioRepasseId ?? Guid.Empty
            },
            Instrucoes = new InstrucoesDePagamentoDto
            {
                Tipo = partes.Length > 0 ? partes[0] : string.Empty,
                Codigo = partes.Length > 1 ? partes[1] : string.Empty,
                ReferenciaExterna = cobranca.ReferenciaExterna
            }
        };
    }

    /// <summary>Separador usado pelo adaptador ao devolver as instrucoes de pagamento.</summary>
    private const char SeparadorDaInstrucao = '|';

    /// <inheritdoc/>
    public async Task<StatusDaCobrancaDto> ObterStatusAsync(Guid pagamentoId)
    {
        var pagamento = await _repo.ObterPorIdAsync(pagamentoId)
            ?? throw new NotFoundException("Pagamento", pagamentoId);

        if (!_usuario.EhAdmin && _usuario.TutorId != pagamento.TutorId)
            throw new AcessoNegadoException("RN-106", "Este pagamento nao pertence ao seu escopo de acesso.");

        var consulta = pagamento.ConsultaId is { } consultaId
            ? await _consultaRepo.ObterPorIdAsync(consultaId)
            : null;

        return new StatusDaCobrancaDto
        {
            PagamentoId = pagamento.Id,
            StatusPagamento = pagamento.StatusPagamento,
            ConsultaId = pagamento.ConsultaId,
            StatusConsulta = consulta?.Status,
            AguardandoConfirmacao = !pagamento.TemDesfecho()
        };
    }

    /// <inheritdoc/>
    public async Task<ResultadoDoWebhookDto> ProcessarWebhookAsync(string payloadBruto, string? tokenDeServico)
    {
        var evento = await _adaptador.ReceberWebhookDeStatusAsync(payloadBruto, tokenDeServico);

        if (!evento.Assinado || string.IsNullOrWhiteSpace(evento.ReferenciaExterna))
            throw new BusinessRuleException("PAGAMENTO-002", "Evento de pagamento invalido ou nao assinado.");

        var pagamento = await _repo.ObterPorReferenciaExternaAsync(evento.ReferenciaExterna)
            ?? throw new NotFoundException("Pagamento nao encontrado para a referencia informada.");

        // Webhook e entregue mais de uma vez por natureza: reprocessar um pagamento que
        // ja teve desfecho nao pode reabrir consulta nem mexer em horario.
        if (pagamento.TemDesfecho())
            return Inalterado(pagamento);

        return evento.Status switch
        {
            StatusPagamento.Confirmado => await ConfirmarAsync(pagamento),
            StatusPagamento.Recusado => await RecusarAsync(pagamento),
            _ => Inalterado(pagamento)
        };
    }

    private static ResultadoDoWebhookDto Inalterado(Pagamento pagamento) => new()
    {
        PagamentoId = pagamento.Id,
        StatusPagamento = pagamento.StatusPagamento,
        ConsultaId = pagamento.ConsultaId,
        Ignorado = true
    };

    /// <summary>
    /// Confirmacao: pagamento confirmado, consulta promovida e horario ocupado em
    /// definitivo (RN-006/RN-035).
    /// </summary>
    private async Task<ResultadoDoWebhookDto> ConfirmarAsync(Pagamento pagamento)
    {
        pagamento.Confirmar();
        _repo.Atualizar(pagamento);
        await _repo.SalvarAsync();

        var consulta = await AtualizarConsultaAsync(pagamento, confirmada: true);

        return new ResultadoDoWebhookDto
        {
            PagamentoId = pagamento.Id,
            StatusPagamento = pagamento.StatusPagamento,
            ConsultaId = consulta?.Id,
            StatusConsulta = consulta?.Status
        };
    }

    /// <summary>
    /// Recusa: o horario travado volta a ficar livre e a consulta expira. Segurar o
    /// horario de quem nao pagou tiraria a vaga de quem pagaria (RN-006/RN-035).
    /// </summary>
    private async Task<ResultadoDoWebhookDto> RecusarAsync(Pagamento pagamento)
    {
        pagamento.Recusar();
        _repo.Atualizar(pagamento);
        await _repo.SalvarAsync();

        var consulta = await AtualizarConsultaAsync(pagamento, confirmada: false);

        return new ResultadoDoWebhookDto
        {
            PagamentoId = pagamento.Id,
            StatusPagamento = pagamento.StatusPagamento,
            ConsultaId = consulta?.Id,
            StatusConsulta = consulta?.Status
        };
    }

    /// <summary>
    /// Propaga o desfecho do pagamento para a consulta e para o horario reservado.
    /// </summary>
    private async Task<Consulta?> AtualizarConsultaAsync(Pagamento pagamento, bool confirmada)
    {
        if (pagamento.ConsultaId is not { } consultaId)
            return null;

        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId);
        if (consulta is null) return null;

        if (confirmada) consulta.ConfirmarPagamento();
        else consulta.Expirar();

        _consultaRepo.Atualizar(consulta);
        await _consultaRepo.SalvarAsync();

        if (consulta.SlotId is { } slotId)
        {
            var slot = await _agendaRepo.ObterSlotAsync(slotId);

            if (slot is not null)
            {
                if (confirmada) slot.Confirmar();
                else slot.Liberar();

                _agendaRepo.AtualizarSlot(slot);
                await _agendaRepo.SalvarAsync();
            }
        }

        return consulta;
    }

    /// <summary>Apura e grava o split do pagamento, quando ele tem consulta vinculada.</summary>
    private async Task<ResultadoDoSplit> ApurarSplitAsync(Pagamento pagamento)
    {
        if (pagamento.ConsultaId is not { } consultaId)
            return default;

        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId);
        if (consulta is null) return default;

        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId);
        if (vet is null) return default;

        var (plano, destinatarioId) = await ResolverPlanoEDestinatarioAsync(vet);

        var strategy = _splitStrategies.FirstOrDefault(s => s.Aplicavel(plano))
            ?? throw new BusinessRuleException("RN-070", "Nao ha regra de split para o plano informado.");

        var split = strategy.Calcular(pagamento.Valor);
        pagamento.RegistrarSplit(split.Plano, split.TakeRate, split.Comissao, split.Repasse, destinatarioId);

        return split;
    }

    /// <summary>
    /// Apura a reparticao da transacao pelo PLANO do prestador (RN-070).
    ///
    /// Ate aqui o criterio era a persona (autonomo 80 / vinculado 60), o que contradizia
    /// a decisao fechada de produto — conflito C-01. O Strategy Pattern permanece; o que
    /// mudou foi o criterio e o fato de haver um unico repasse (RN-072).
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

        var (plano, destinatarioId) = await ResolverPlanoEDestinatarioAsync(vet);

        var strategy = _splitStrategies.FirstOrDefault(s => s.Aplicavel(plano))
            ?? throw new BusinessRuleException("RN-070", $"Nao ha regra de split para o plano {plano}.");

        var split = strategy.Calcular(pagamento.Valor);

        pagamento.RegistrarSplit(split.Plano, split.TakeRate, split.Comissao, split.Repasse, destinatarioId);
        _repo.Atualizar(pagamento);
        await _repo.SalvarAsync();
        return MapearParaDto(pagamento);
    }

    /// <summary>
    /// Descobre o plano que rege a transacao e quem recebe o repasse (RN-070/RN-072).
    ///
    /// Vet vinculado: vale o plano da CLINICA, que e quem assina — o Enterprise, por
    /// exemplo, e precificado por numero de vets da unidade. O repasse vai a ela, e a
    /// remuneracao interna do profissional fica fora do escopo da plataforma.
    /// Vet autonomo: o proprio plano, e o repasse vai direto a ele.
    /// </summary>
    private async Task<(PlanoAssinatura Plano, Guid DestinatarioId)> ResolverPlanoEDestinatarioAsync(Veterinario vet)
    {
        if (vet.EmpresaId is not { } empresaId)
            return (vet.Plano, vet.Id);

        var empresa = await _empresaRepo.ObterPorIdAsync(empresaId);

        return empresa is null
            ? (vet.Plano, vet.Id)
            : (empresa.Plano, empresa.Id);
    }

    private static PagamentoDto MapearParaDto(Pagamento p) => new()
    {
        Id = p.Id, TutorId = p.TutorId, ConsultaId = p.ConsultaId, InternacaoId = p.InternacaoId,
        Valor = p.Valor, MeioPagamento = p.MeioPagamento, Momento = p.Momento,
        StatusPagamento = p.StatusPagamento, PercentualSplit = p.PercentualSplit,
        ValorEstornado = p.ValorEstornado,
        PlanoAplicado = p.PlanoAplicado, TakeRate = p.TakeRate,
        Comissao = p.Comissao, Repasse = p.Repasse,
        DestinatarioRepasseId = p.DestinatarioRepasseId
    };
}
