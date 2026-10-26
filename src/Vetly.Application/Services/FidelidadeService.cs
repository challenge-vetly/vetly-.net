using Microsoft.Extensions.Logging;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Observability;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>
/// Programa de fidelidade: pontos por serviço pago e por obrigação cumprida, tier com
/// multiplicador, resgate em cupom e expiração FIFO (RN-046 a RN-054).
///
/// O saldo é a soma dos lançamentos, e não um campo que alguém atualiza. Saldo
/// guardado à parte diverge do extrato no primeiro erro, e aí não há como saber qual
/// dos dois está certo.
/// </summary>
public class FidelidadeService : IFidelidadeService
{
    private readonly IFidelidadeRepository _repo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly INotificacaoService _notificacoes;
    private readonly IUsuarioAtual _usuario;
    private readonly IItemMarketplaceRepository _itens;
    private readonly ILogger<FidelidadeService> _logger;

    public FidelidadeService(
        IFidelidadeRepository repo,
        IConsultaRepository consultaRepo,
        IPagamentoRepository pagamentoRepo,
        INotificacaoService notificacoes,
        IUsuarioAtual usuario,
        IItemMarketplaceRepository itens,
        ILogger<FidelidadeService> logger)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _pagamentoRepo = pagamentoRepo;
        _notificacoes = notificacoes;
        _usuario = usuario;
        _itens = itens;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SaldoDePontosDto> ObterSaldoAsync(Guid tutorId)
    {
        GarantirEscopo(tutorId);

        var movimentos = (await _repo.ObterDoTutorAsync(tutorId)).ToList();
        var agora = DateTime.UtcNow;

        var saldo = movimentos.Sum(m => m.Pontos);
        var tier = TierDe(movimentos, agora);

        // O que vence nos próximos 30 dias, para o Responsável usar antes de perder.
        // Conta o restante do lote, não o crédito original: ponto já gasto não vence.
        var limite = agora.AddDays(30);

        var vencendo = movimentos
            .Where(m => m.Tipo == TipoMovimentoDePontos.Credito
                        && m.Restante > 0
                        && m.ExpiraEm is { } expira && expira <= limite && expira > agora)
            .Sum(m => m.Restante);

        return new SaldoDePontosDto
        {
            TutorId = tutorId,
            Saldo = saldo,
            ValorEmReais = RegrasDeFidelidade.EmReais(saldo),
            PontosVencendoEm30Dias = vencendo,
            Tier = tier,
            Multiplicador = RegrasDeFidelidade.MultiplicadorDe(tier),
            AcumuloEm12Meses = AcumuloDe(movimentos, agora),
            PontosParaProximoTier = PontosParaProximoTier(AcumuloDe(movimentos, agora))
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<MovimentoDePontosDto>> ObterExtratoAsync(Guid tutorId)
    {
        GarantirEscopo(tutorId);

        var movimentos = await _repo.ObterDoTutorAsync(tutorId);

        return movimentos.OrderByDescending(m => m.OcorridoEm).Select(Mapear);
    }

    /// <inheritdoc/>
    public async Task<MovimentoDePontosDto?> CreditarPorConsultaAsync(Guid consultaId)
    {
        // Job reentregue não credita duas vezes pelo mesmo atendimento
        if (await _repo.ObterCreditoDaConsultaAsync(consultaId) is not null)
            return null;

        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        // RN-052: só pontuam eventos com consulta confirmada E realizada. Consulta
        // cancelada ou com pagamento recusado não vira crédito, senão o programa
        // pagaria por receita que não entrou.
        if (consulta.Status != StatusConsulta.Realizada)
            return null;

        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(consultaId);

        if (pagamento is null || pagamento.StatusPagamento != StatusPagamento.Confirmado || pagamento.Valor <= 0)
            return null;

        var tier = await TierDoTutorAsync(consulta.TutorId);

        var movimento = MovimentoDePontos.PorServicoPago(
            consulta.TutorId, consultaId, pagamento.Valor, tier);

        await _repo.AdicionarAsync(movimento);
        await _repo.SalvarAsync();

        await AvisarCreditoAsync(consulta.TutorId, movimento, tier, "o atendimento", consulta.AnimalId);

        return Mapear(movimento);
    }

    /// <inheritdoc/>
    public async Task<MovimentoDePontosDto?> CreditarPorObrigacaoAsync(
        Guid tutorId, Guid obrigacaoId, string descricao)
    {
        // A mesma obrigação não credita duas vezes, ainda que seja cumprida de novo
        // num ciclo seguinte — o crédito é do cumprimento, e o próximo ciclo gera
        // outra obrigação.
        if (await _repo.ObterCreditoDaObrigacaoAsync(obrigacaoId) is not null)
            return null;

        var tier = await TierDoTutorAsync(tutorId);

        var movimento = MovimentoDePontos.PorObrigacaoCumprida(tutorId, obrigacaoId, descricao, tier);

        await _repo.AdicionarAsync(movimento);
        await _repo.SalvarAsync();

        await AvisarCreditoAsync(tutorId, movimento, tier, descricao, animalId: null);

        return Mapear(movimento);
    }

    /// <summary>
    /// Avisa o crédito e, quando houver, a subida de tier (RN-016/RN-048, §6.2).
    ///
    /// O tier é recalculado <b>depois</b> do crédito e comparado com o que valia
    /// antes: é esse crédito que pode ter cruzado a faixa, e anunciar "você é Prata"
    /// em toda notificação transformaria a conquista em ruído de rodapé.
    ///
    /// Os dois eventos vão numa notificação só, e não em duas. A matriz de canais
    /// (§6.2) trata "pontos creditados / mudança de tier" como uma linha, e são o
    /// mesmo fato para quem recebe: cumpri algo, ganhei, e por isso subi.
    /// </summary>
    private async Task AvisarCreditoAsync(
        Guid tutorId, MovimentoDePontos movimento, TierFidelidade tierAntes,
        string origem, Guid? animalId)
    {
        var tierDepois = await TierDoTutorAsync(tutorId);
        var subiu = tierDepois > tierAntes;

        var corpo = $"Voce ganhou {movimento.Pontos} ponto(s) por {origem}.";

        if (movimento.Multiplicador > 1m)
            corpo += $" Multiplicador {tierAntes} de {movimento.Multiplicador:0.##}x aplicado.";

        if (subiu)
            corpo += $" E subiu para o tier {tierDepois}.";

        await _notificacoes.CriarAsync(new CriarNotificacaoDto
        {
            TutorId = tutorId,
            Tipo = TipoNotificacao.PontosCreditados,
            Titulo = subiu ? $"Voce chegou ao tier {tierDepois}" : "Pontos creditados",
            Corpo = corpo,
            AnimalId = animalId,
            ConsultaId = movimento.ConsultaId,
            Destino = "/fidelidade"
        });
    }

    /// <inheritdoc/>
    public async Task<SimulacaoDeResgateDto> SimularResgateAsync(Guid tutorId, SimularResgateDto dto)
    {
        GarantirEscopo(tutorId);

        var item = await ResolverItemAsync(dto);

        return await SimularAsync(tutorId, item);
    }

    /// <summary>
    /// Calcula o resgate de um item já resolvido: saldo, desconto e divisão
    /// (RN-049/RN-050/RN-051).
    /// </summary>
    private async Task<SimulacaoDeResgateDto> SimularAsync(Guid tutorId, ItemDoResgate item)
    {
        var movimentos = (await _repo.ObterDoTutorAsync(tutorId)).ToList();
        var saldo = movimentos.Sum(m => m.Pontos);

        if (item.Pontos > saldo)
            throw new BusinessRuleException("RN-050",
                $"Saldo insuficiente: {saldo} ponto(s) disponivel(is).");

        var desconto = RegrasDeFidelidade.EmReais(item.Pontos);
        var (vetly, prestador, faixa) = RegrasDeFidelidade.Dividir(desconto);

        return new SimulacaoDeResgateDto
        {
            ItemId = item.Catalogo?.Id,
            ItemRef = item.ItemRef,
            ItemNome = item.ItemNome,
            Categoria = item.Categoria,
            PontosADebitar = item.Pontos,
            Desconto = desconto,
            Faixa = faixa,
            PercentualVetly = RegrasDeFidelidade.PercentualVetlyDe(faixa),
            PercentualPrestador = 100m - RegrasDeFidelidade.PercentualVetlyDe(faixa),
            ValorVetly = vetly,
            ValorPrestador = prestador,
            ValidadeDias = (int)RegrasDeFidelidade.ValidadeDoCupom.TotalDays,
            SaldoApos = saldo - item.Pontos,

            // RN-051: no MVP a divisão é calculada, gravada e exibida, sem abatimento
            // financeiro real. Dizer isso na resposta evita que o app prometa desconto
            // que não vai acontecer no caixa.
            Abatimento = "Simulado"
        };
    }

    /// <inheritdoc/>
    public async Task<CupomDto> ResgatarAsync(Guid tutorId, SimularResgateDto dto)
    {
        GarantirEscopo(tutorId);

        var item = await ResolverItemAsync(dto);
        var simulacao = await SimularAsync(tutorId, item);

        // RN-109: a unidade e reservada no mesmo SaveChanges que debita os pontos e
        // emite o cupom. Se o saldo nao fecha, nada e gravado — nem a reserva. E o
        // ESTOQUE e token de concorrencia: dois resgates da ultima unidade nao saem.
        if (item.Catalogo is { } catalogo)
        {
            catalogo.ReservarUnidade();
            _itens.Atualizar(catalogo);
        }

        var cupom = new CupomResgate(
            tutorId, item.ItemRef, item.ItemNome, item.Categoria, item.Pontos, simulacao.Desconto,
            item.Catalogo?.Id);

        // RN-050: o débito consome os lotes em FIFO — o ponto mais antigo primeiro,
        // que é o que está mais perto de vencer. Consumir o mais novo deixaria o
        // Responsável perder pontos que ele acabou de usar para pagar.
        await ConsumirFifoAsync(tutorId, item.Pontos);

        await _repo.AdicionarCupomAsync(cupom);
        await _repo.AdicionarAsync(MovimentoDePontos.PorResgate(
            tutorId, item.Pontos, simulacao.Desconto, cupom.Id));

        await _repo.SalvarAsync();

        if (item.Catalogo is not null)
            VetlyTelemetry.ResgatesNoMarketplace.Add(1,
                new KeyValuePair<string, object?>("categoria", item.Categoria.ToString()));

        return Mapear(cupom);
    }

    /// <summary>O que está sendo resgatado, já decidido pelo servidor ou pela forma legada.</summary>
    /// <param name="Catalogo">Item do catálogo; nulo na forma legada.</param>
    private sealed record ItemDoResgate(
        ItemMarketplace? Catalogo, string ItemRef, string? ItemNome, CategoriaItem Categoria, int Pontos);

    /// <summary>
    /// Resolve o item do pedido. Com <c>itemId</c>, tudo vem do catálogo e os demais
    /// campos do pedido são ignorados (RN-108). Sem ele, vale a forma legada — o
    /// cliente dizendo quantos pontos debitar —, aceita para não quebrar o app antigo
    /// e registrada em log para medir quando pode sair.
    /// </summary>
    private async Task<ItemDoResgate> ResolverItemAsync(SimularResgateDto dto)
    {
        if (dto.ItemId is { } itemId)
        {
            var item = await _itens.ObterPorIdAsync(itemId)
                ?? throw new NotFoundException("ItemMarketplace", itemId);

            if (!item.Ativo)
                throw new BusinessRuleException("RN-108", "Este item saiu do catalogo e nao pode ser resgatado.");

            if (!item.Disponivel)
                throw new BusinessRuleException("RN-108", "Este item esta sem estoque no momento.");

            return new ItemDoResgate(item, item.Id.ToString(), item.Nome, item.Categoria, item.CustoEmPontos);
        }

        if (dto.Pontos <= 0)
            throw new ValidationException("pontos", "O resgate deve debitar pontos.");

        _logger.LogWarning(
            "Resgate na forma legada (sem itemId): itemRef {ItemRef}, {Pontos} pontos informados pelo cliente",
            dto.ItemRef, dto.Pontos);

        return new ItemDoResgate(null, dto.ItemRef, dto.ItemNome, dto.Categoria, dto.Pontos);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<CupomDto>> ObterCuponsAsync(Guid tutorId)
    {
        GarantirEscopo(tutorId);

        var cupons = await _repo.ObterCuponsDoTutorAsync(tutorId);

        return cupons.Select(Mapear);
    }

    /// <inheritdoc/>
    public async Task<CupomDto> ObterCupomAsync(Guid cupomId)
    {
        var cupom = await _repo.ObterCupomAsync(cupomId)
            ?? throw new NotFoundException("Cupom", cupomId);

        GarantirEscopo(cupom.TutorId);

        return Mapear(cupom);
    }

    /// <inheritdoc/>
    public async Task MarcarCupomComoUsadoAsync(Guid cupomId)
    {
        var cupom = await _repo.ObterCupomAsync(cupomId)
            ?? throw new NotFoundException("Cupom", cupomId);

        // RN-054: um cupom vale para um item e uma transacao. Reaplica-lo empilharia
        // desconto sobre a mesma margem.
        cupom.Resgatar(DateTime.UtcNow);

        _repo.AtualizarCupom(cupom);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task ReverterUsoDoCupomAsync(Guid cupomId)
    {
        var cupom = await _repo.ObterCupomAsync(cupomId);

        // Cupom inexistente nao e erro aqui: este caminho roda dentro do webhook, e
        // derrubar o processamento do pagamento por causa do cupom deixaria o
        // pagamento sem desfecho — que e o dado que importa.
        if (cupom is null)
            return;

        cupom.ReverterUso();

        _repo.AtualizarCupom(cupom);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<int> EstornarPorConsultaAsync(Guid consultaId)
    {
        var credito = await _repo.ObterCreditoDaConsultaAsync(consultaId);

        if (credito is null)
            return 0;

        // Já estornado: cancelar duas vezes não pode debitar duas vezes
        if (await _repo.ObterEstornoDaConsultaAsync(consultaId) is not null)
            return 0;

        // RN-052: o estorno tira o que ainda não foi gasto. Cobrar de volta ponto já
        // resgatado deixaria o saldo negativo por algo que o Responsável usou de
        // boa-fé antes do cancelamento.
        var aEstornar = credito.Consumir(credito.Restante);

        if (aEstornar == 0)
            return 0;

        _repo.Atualizar(credito);

        await _repo.AdicionarAsync(
            MovimentoDePontos.PorEstorno(credito.TutorId, aEstornar, consultaId));

        await _repo.SalvarAsync();

        return aEstornar;
    }

    /// <inheritdoc/>
    public async Task<int> ExpirarVencidosAsync()
    {
        var agora = DateTime.UtcNow;
        var vencidos = (await _repo.ObterCreditosVencidosSemBaixaAsync(agora)).ToList();

        var expirados = 0;

        foreach (var credito in vencidos)
        {
            // Só o que sobrou do lote expira: o que já foi gasto saiu no débito
            var aExpirar = credito.Consumir(credito.Restante);

            if (aExpirar == 0)
                continue;

            _repo.Atualizar(credito);

            await _repo.AdicionarAsync(
                MovimentoDePontos.PorExpiracao(credito.TutorId, aExpirar, credito.Id));

            expirados += aExpirar;
        }

        // Cupom vencido também é baixado aqui: os pontos não voltam (RN-053), mas o
        // cupom precisa parar de aparecer como utilizável no app.
        foreach (var cupom in await _repo.ObterCuponsVencidosAsync(agora))
        {
            // RN-109: a unidade do catalogo volta ao estoque, so na transicao — a
            // validacao do QR pode ter expirado este mesmo cupom antes
            if (cupom.Expirar() && cupom.ItemMarketplaceId is { } itemId
                && await _itens.ObterPorIdAsync(itemId) is { } item)
            {
                item.DevolverUnidade();
                _itens.Atualizar(item);
            }

            _repo.AtualizarCupom(cupom);
        }

        await _repo.SalvarAsync();

        return expirados;
    }

    /// <summary>
    /// Consome os lotes em FIFO (RN-050) — o crédito mais antigo primeiro, que é o que
    /// está mais perto de vencer.
    /// </summary>
    private async Task ConsumirFifoAsync(Guid tutorId, int pontos)
    {
        var lotes = (await _repo.ObterLotesComSaldoAsync(tutorId))
            .OrderBy(m => m.ExpiraEm)
            .ThenBy(m => m.OcorridoEm);

        var restante = pontos;

        foreach (var lote in lotes)
        {
            if (restante <= 0)
                break;

            restante -= lote.Consumir(restante);
            _repo.Atualizar(lote);
        }

        if (restante > 0)
            throw new BusinessRuleException("RN-050",
                "Saldo insuficiente para o resgate.");
    }

    /// <summary>
    /// Tier a partir do que foi <b>creditado</b> nos últimos 12 meses (RN-048).
    ///
    /// Conta o crédito, não o saldo: quem resgatou não perde o tier por ter usado o
    /// programa — usar é exatamente o comportamento que o programa quer.
    /// </summary>
    private static int AcumuloDe(IEnumerable<MovimentoDePontos> movimentos, DateTime agora)
    {
        var desde = agora.Subtract(RegrasDeFidelidade.JanelaDoTier);

        return movimentos
            .Where(m => m.Tipo == TipoMovimentoDePontos.Credito && m.OcorridoEm >= desde)
            .Sum(m => m.Pontos);
    }

    private static TierFidelidade TierDe(IEnumerable<MovimentoDePontos> movimentos, DateTime agora) =>
        RegrasDeFidelidade.TierPara(AcumuloDe(movimentos, agora));

    private async Task<TierFidelidade> TierDoTutorAsync(Guid tutorId)
    {
        var movimentos = await _repo.ObterDoTutorAsync(tutorId);

        return TierDe(movimentos, DateTime.UtcNow);
    }

    /// <summary>Quanto falta para subir de faixa. Zero no Ouro, que é o topo.</summary>
    private static int PontosParaProximoTier(int acumulo) => acumulo switch
    {
        >= 3000 => 0,
        >= 1000 => 3000 - acumulo,
        _ => 1000 - acumulo
    };

    /// <summary>Os pontos são do Responsável: o escopo vem do token (RN-105/RN-106).</summary>
    private void GarantirEscopo(Guid tutorId)
    {
        if (_usuario.EhAdmin || _usuario.TutorId == tutorId)
            return;

        throw new AcessoNegadoException("RN-106", "Estes pontos nao pertencem ao seu escopo de acesso.");
    }

    private static MovimentoDePontosDto Mapear(MovimentoDePontos m) => new()
    {
        Id = m.Id,
        Tipo = m.Tipo,
        Pontos = m.Pontos,
        PontosBrutos = m.PontosBrutos,
        Multiplicador = m.Multiplicador,
        Restante = m.Restante,
        ConsultaId = m.ConsultaId,
        ObrigacaoId = m.ObrigacaoId,
        CupomId = m.CupomId,
        ValorEmReais = m.ValorEmReais,
        ExpiraEm = m.ExpiraEm,
        Descricao = m.Descricao,
        OcorridoEm = m.OcorridoEm
    };

    /// <summary>
    /// Cupom para DTO. Interno ao assembly porque a validação do QR
    /// (<see cref="MarketplaceService"/>) devolve o mesmo formato.
    /// </summary>
    internal static CupomDto Mapear(CupomResgate c) => new()
    {
        Id = c.Id,
        CodigoQr = c.CodigoQr,
        ItemRef = c.ItemRef,
        ItemMarketplaceId = c.ItemMarketplaceId,
        ItemNome = c.ItemNome,
        Categoria = c.Categoria,
        PontosDebitados = c.PontosDebitados,
        Desconto = c.Desconto,
        Faixa = c.Faixa,
        DescontoVetly = c.DescontoVetly,
        DescontoPrestador = c.DescontoPrestador,
        Status = c.Status,
        EmitidoEm = c.EmitidoEm,
        ExpiraEm = c.ExpiraEm,
        ResgatadoEm = c.ResgatadoEm
    };
}
