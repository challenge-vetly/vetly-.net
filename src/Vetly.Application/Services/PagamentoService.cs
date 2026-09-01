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
    private readonly IFilaDeJobs _fila;
    private readonly IEnumerable<ISplitFinanceiroStrategy> _splitStrategies;
    private readonly IFidelidadeService _fidelidade;
    private readonly IUsuarioAtual _usuario;

    public PagamentoService(
        IPagamentoRepository repo,
        IVeterinarioRepository vetRepo,
        IConsultaRepository consultaRepo,
        IEmpresaRepository empresaRepo,
        IPagamentoAdapter adaptador,
        IAgendaRepository agendaRepo,
        IFilaDeJobs fila,
        IEnumerable<ISplitFinanceiroStrategy> splitStrategies,
        IFidelidadeService fidelidade,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _consultaRepo = consultaRepo;
        _empresaRepo = empresaRepo;
        _adaptador = adaptador;
        _agendaRepo = agendaRepo;
        _fila = fila;
        _splitStrategies = splitStrategies;
        _fidelidade = fidelidade;
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
        var (tutorId, valor) = await ValidarPedidoDeCobrancaAsync(dto);

        var pagamento = new Pagamento(tutorId, valor, dto.MeioPagamento, dto.ConsultaId, dto.InternacaoId);
        pagamento.DefinirTipo(dto.InternacaoId is null ? TipoPagamento.Consulta : TipoPagamento.Caucao);

        // O split e calculado pela Vetly, nunca pelo provedor (RN-051/RN-070)
        var split = await ApurarSplitAsync(pagamento);

        // RN-051: o desconto do cupom e repartido entre Vetly e prestador pela faixa
        // do valor. Nao reduz o bruto: reduz a comissao e o repasse, cada um na sua
        // parte.
        if (dto.CupomId is { } cupomId)
        {
            // RN-051: a fidelidade e da consulta. Internacao tem caucao e saldo
            // apurados por procedimento, e nao passa pelo split que financia o
            // desconto — aplicar cupom ali abateria de uma comissao que nao existe.
            if (dto.InternacaoId is not null)
                throw new BusinessRuleException("RN-051",
                    "Cupom de fidelidade nao se aplica a internacao.");

            split = await AplicarCupomAsync(pagamento, split, cupomId);
        }

        var chave = pagamento.Id.ToString();
        var cobranca = await _adaptador.CriarCobrancaAsync(
            new CriarCobrancaRequest(chave, pagamento.Id, pagamento.ValorCobrado, pagamento.MeioPagamento));

        pagamento.RegistrarCobranca(cobranca.ReferenciaExterna, chave);

        await _repo.AdicionarAsync(pagamento);
        await _repo.SalvarAsync();

        // O adaptador simulado devolve o evento que um provedor mandaria; enfileira-lo
        // faz o webhook chegar sozinho, com o atraso de um provedor de verdade.
        // O adaptador real nao devolve evento nenhum: quem manda e o provedor.
        if (cobranca.EventoSimulado is { } evento)
            await _fila.EnfileirarAsync(TipoJob.ConfirmarPagamentoSimulado, evento, cobranca.AtrasoDoEvento);

        var partes = cobranca.Instrucoes.Split(SeparadorDaInstrucao);

        return new CobrancaCriadaRespostaDto
        {
            Id = pagamento.Id,
            StatusPagamento = pagamento.StatusPagamento,
            Valor = pagamento.Valor,
            ValorCobrado = pagamento.ValorCobrado,
            DescontoFidelidade = pagamento.CupomId is { } cupom
                ? new DescontoDeFidelidadeDto
                {
                    CupomId = cupom,
                    PontosResgatados = pagamento.PontosResgatados ?? 0,
                    Valor = pagamento.ValorDoDesconto ?? 0m,
                    Faixa = pagamento.FaixaDoDesconto,
                    AbsorvidoVetly = pagamento.DescontoVetly ?? 0m,
                    AbsorvidoPrestador = pagamento.DescontoPrestador ?? 0m
                }
                : null,
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

    /// <summary>
    /// Confere o pedido de cobranca antes de qualquer coisa sair para o provedor, e
    /// devolve o Responsavel e o valor que de fato valem.
    ///
    /// Sem isto, <c>POST /api/pagamentos</c> era uma rota em que o cliente escolhia
    /// quem paga, quanto paga e por qual atendimento — as tres coisas que o servidor
    /// tem de decidir sozinho.
    /// </summary>
    private async Task<(Guid TutorId, decimal Valor)> ValidarPedidoDeCobrancaAsync(CriarPagamentoDto dto)
    {
        // O pagador vem do token. Cobrar em nome de outro Responsavel geraria divida
        // no nome de quem nao pediu nada (RN-106).
        var tutorId = _usuario.EhTutor && _usuario.TutorId is { } doToken ? doToken : dto.TutorId;

        if (tutorId == Guid.Empty)
            throw new BusinessRuleException("RN-006", "A cobranca precisa de um Responsavel.");

        // Cobranca de internacao vem do InternacaoService, que ja apurou o valor pelos
        // procedimentos do dia (RN-100/RN-101). Nao ha servico de catalogo ali.
        if (dto.InternacaoId is not null)
            return (tutorId, dto.Valor);

        if (dto.ConsultaId is not { } consultaId)
            return (tutorId, dto.Valor);

        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (consulta.TutorId != tutorId)
            throw new AcessoNegadoException("RN-106", "Esta consulta nao pertence ao seu escopo de acesso.");

        // Cobrar consulta ja realizada, cancelada ou expirada seria cobrar por um
        // atendimento que nao vai acontecer, ou por um que ja foi pago (RN-006).
        if (consulta.Status is not (StatusConsulta.EmCheckout or StatusConsulta.Confirmada))
            throw new ConflitoDeEstadoException("RN-006",
                $"Consulta com status {consulta.Status} nao aceita nova cobranca.");

        // Duas cobrancas para a mesma consulta cobrariam duas vezes pelo mesmo
        // atendimento. A recusada nao conta: ela e justamente a que precisa de outra.
        var existente = await _repo.ObterPorConsultaAsync(consultaId);

        if (existente is not null && existente.StatusPagamento != StatusPagamento.Recusado)
            throw new ConflitoDeEstadoException("RN-006",
                "Esta consulta ja tem uma cobranca em aberto ou confirmada.");

        // O lock do horario tem de estar valendo. Cobrar por um slot que ja voltou a
        // fila entregaria dinheiro sem entregar horario (RN-035).
        if (consulta.SlotId is { } slotId)
        {
            var slot = await _agendaRepo.ObterSlotAsync(slotId);

            if (slot is null || slot.LockConsultaId != consulta.Id)
                throw new ConflitoDeEstadoException("RN-035",
                    "Este horario acabou de ser reservado por outra pessoa.");
        }

        // O preco e o do catalogo, nunca o do corpo da requisicao: aceitar o valor do
        // cliente e aceitar que ele pague o que quiser (RN-032).
        if (consulta.ServicoId is { } servicoId)
        {
            var servico = await _agendaRepo.ObterServicoAsync(servicoId)
                ?? throw new NotFoundException("Servico", servicoId);

            return (tutorId, servico.Valor);
        }

        return (tutorId, dto.Valor);
    }

    /// <inheritdoc/>
    public async Task<CarteiraDoTutorDto> ObterCarteiraAsync(Guid tutorId)
    {
        // RN-106: a carteira e do proprio Responsavel. O Admin alcanca pelo
        // consolidado da unidade, que e outra visao e outro escopo.
        if (!_usuario.EhAdmin && _usuario.TutorId != tutorId)
            throw new AcessoNegadoException("RN-106", "Esta carteira nao pertence ao seu escopo de acesso.");

        var pagamentos = (await _repo.ObterPorTutorAsync(tutorId)).ToList();

        var lancamentos = pagamentos
            .OrderByDescending(p => p.Momento)
            .Select(p => new LancamentoDaCarteiraDto
            {
                PagamentoId = p.Id,
                ConsultaId = p.ConsultaId,
                InternacaoId = p.InternacaoId,
                Tipo = p.Tipo,
                MeioPagamento = p.MeioPagamento,
                Status = p.StatusPagamento,
                Valor = p.Valor,
                Desconto = p.ValorDoDesconto,
                ValorCobrado = p.ValorCobrado,
                ValorEstornado = p.ValorEstornado,
                Momento = p.Momento
            })
            .ToList();

        // So transacao confirmada soma no total pago: cobranca pendente ainda nao saiu
        // do bolso de ninguem, e recusada nunca vai sair.
        var confirmados = pagamentos.Where(p => p.StatusPagamento == StatusPagamento.Confirmado).ToList();

        return new CarteiraDoTutorDto
        {
            TutorId = tutorId,
            TotalPago = confirmados.Sum(p => p.ValorCobrado),
            TotalEstornado = pagamentos.Sum(p => p.ValorEstornado ?? 0m),
            TotalDeDescontos = confirmados.Sum(p => p.ValorDoDesconto ?? 0m),
            Liquidacao = "Simulada",
            Lancamentos = lancamentos
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

        // O cupom foi marcado como usado na criacao da cobranca, para que o
        // Responsavel visse o desconto antes de decidir pagar. Se o pagamento nao
        // vingou, o desconto nao foi usado: manter o cupom queimado cobraria os pontos
        // por um beneficio que ninguem recebeu (RN-053).
        if (pagamento.CupomId is { } cupomId)
            await _fidelidade.ReverterUsoDoCupomAsync(cupomId);

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

            // So mexe no horario que ESTA consulta esta segurando. O webhook e
            // assincrono e pode chegar depois de o lock ter expirado e o slot ter sido
            // tomado por outra pessoa — confirmar ali daria o horario ao pagamento
            // atrasado, e liberar ali derrubaria a reserva de quem chegou legitimamente
            // depois. Nos dois casos a vitima e quem nao errou nada (RN-035).
            if (slot is not null && slot.LockConsultaId == consulta.Id)
            {
                if (confirmada) slot.Confirmar();
                else slot.Liberar();

                _agendaRepo.AtualizarSlot(slot);
                await _agendaRepo.SalvarAsync();
            }
        }

        return consulta;
    }

    /// <summary>
    /// Aplica o resgate de pontos e reapura o split (RN-051).
    ///
    /// O desconto é abatido <b>da comissão</b>, não do bruto. O teto é a própria
    /// comissão: a Vetly banca a fidelidade que oferece, mas não paga para atender.
    /// O repasse ao prestador não muda em nenhum dos casos (RN-072).
    /// </summary>
    private async Task<ResultadoDoSplit> AplicarCupomAsync(
        Pagamento pagamento, ResultadoDoSplit split, Guid cupomId)
    {
        var cupom = await _fidelidade.ObterCupomAsync(cupomId);


        if (cupom.Status != StatusCupom.Emitido || cupom.ExpiraEm <= DateTime.UtcNow)
            throw new BusinessRuleException("RN-053",
                "Este cupom nao esta mais valido.");

        if (cupom.Desconto > pagamento.Valor)
            throw new BusinessRuleException("RN-054",
                "O desconto do cupom excede o valor da cobranca.");

        // A Vetly banca a fidelidade que oferece, mas nao paga para atender: a parte
        // dela nunca pode passar da propria comissao. Sem esta guarda, um cupom grande
        // numa consulta barata produziria comissao negativa — a plataforma pagando
        // para que a consulta acontecesse (RN-051/RN-070).
        if (cupom.DescontoVetly > split.Comissao)
            throw new BusinessRuleException("RN-051",
                "O desconto do cupom excede a comissao desta transacao.");

        // A parte de cada um sai do proprio bolso: a da Vetly, da comissao; a do
        // prestador, do repasse. Somadas, fecham o desconto — e o bruto continua
        // sendo o preco do servico.
        pagamento.AplicarDesconto(
            cupom.Id, cupom.PontosDebitados, cupom.Desconto,
            cupom.DescontoVetly, cupom.DescontoPrestador, cupom.Faixa);

        var comissao = split.Comissao - cupom.DescontoVetly;
        var repasse = split.Repasse - cupom.DescontoPrestador;

        pagamento.RegistrarSplit(
            split.Plano, split.TakeRate, comissao, repasse,
            pagamento.DestinatarioRepasseId ?? Guid.Empty);

        await _fidelidade.MarcarCupomComoUsadoAsync(cupom.Id);

        return split with { Comissao = comissao, Repasse = repasse };
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

        // O split e recalculado sobre o BRUTO, porque o take rate incide sobre o preco
        // do servico. Mas o desconto ja concedido tem de ser reabatido: sem isto,
        // reprocessar o split de um pagamento com cupom devolveria a comissao inteira
        // a Vetly e o repasse inteiro ao prestador — ninguem pagaria a fidelidade que
        // o Responsavel ja recebeu (RN-051/RN-072).
        var comissao = split.Comissao - (pagamento.DescontoVetly ?? 0m);
        var repasse = split.Repasse - (pagamento.DescontoPrestador ?? 0m);

        pagamento.RegistrarSplit(split.Plano, split.TakeRate, comissao, repasse, destinatarioId);
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
