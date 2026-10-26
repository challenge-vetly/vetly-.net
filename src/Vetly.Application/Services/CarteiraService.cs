using Vetly.Application.DTOs.Carteira;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Histórico unificado da carteira (RN-115).
///
/// <para>
/// <b>Serviço próprio, e não mais um método do <see cref="PagamentoService"/>.</b> Este
/// é um modelo de leitura que cruza duas tabelas — pagamentos e cupons — e não
/// escreve nada. O <c>PagamentoService</c> é dono da cobrança, do webhook e do split,
/// com o ciclo de escrita que isso implica; misturar ali uma projeção sobre cupons o
/// faria depender da fidelidade por um motivo que não é dele.
/// </para>
/// <para>
/// <b>Paginação em memória, de propósito.</b> Os lançamentos saem de duas tabelas com
/// formatos diferentes, e o reembolso é uma segunda linha derivada do mesmo
/// pagamento. Paginar no banco exigiria uma view com <c>UNION</c> mantida à parte do
/// modelo do EF. O volume é por Responsável — dezenas a poucas centenas de linhas
/// por ano —, então ler tudo do tutor, unir, ordenar e recortar custa menos que a
/// view custaria para manter. Se um tutor um dia tiver milhares de lançamentos, é
/// aqui que a view entra.
/// </para>
/// </summary>
public class CarteiraService : ICarteiraService
{
    private readonly IPagamentoRepository _pagamentos;
    private readonly IFidelidadeRepository _fidelidade;
    private readonly IUsuarioAtual _usuario;

    public CarteiraService(IPagamentoRepository pagamentos, IFidelidadeRepository fidelidade, IUsuarioAtual usuario)
    {
        _pagamentos = pagamentos;
        _fidelidade = fidelidade;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<HistoricoDaCarteiraDto> ObterHistoricoAsync(
        Guid tutorId, FiltroHistoricoDaCarteiraDto filtro, Paginacao paginacao, Ordenacao? ordenacao = null)
    {
        // RN-106: escopo do token. O id da rota so escolhe a carteira quando quem pede
        // e o Admin; para o Responsavel, so vale se for o dele.
        if (!_usuario.EhAdmin && _usuario.TutorId != tutorId)
            throw new AcessoNegadoException("RN-106", "Esta carteira nao pertence ao seu escopo de acesso.");

        var criterio = (ordenacao ?? new Ordenacao()).Resolver(
            CamposDeOrdenacao.Carteira.Todos, CamposDeOrdenacao.Carteira.Momento, descendentePadrao: true);

        var pagamentos = (await _pagamentos.ObterPorTutorAsync(tutorId)).ToList();
        var cupons = (await _fidelidade.ObterCuponsDoTutorAsync(tutorId)).ToList();

        var filtrados = Lancamentos(pagamentos, cupons).Where(l => Atende(l, filtro)).ToList();

        var ordenados = (criterio.Campo, criterio.Descendente) switch
        {
            (CamposDeOrdenacao.Carteira.Valor, false) => filtrados.OrderBy(l => l.ValorEmReais ?? 0m).ThenByDescending(l => l.Momento),
            (CamposDeOrdenacao.Carteira.Valor, true) => filtrados.OrderByDescending(l => l.ValorEmReais ?? 0m).ThenByDescending(l => l.Momento),
            (_, false) => filtrados.OrderBy(l => l.Momento),
            (_, true) => filtrados.OrderByDescending(l => l.Momento)
        };

        var pagina = ordenados
            .ThenBy(l => l.Tipo)
            .Skip(paginacao.Deslocamento)
            .Take(paginacao.Tamanho)
            .ToList();

        return new HistoricoDaCarteiraDto(tutorId, pagina, filtrados.Count, paginacao, Resumir(filtrados, pagamentos));
    }

    /// <summary>
    /// Todos os lançamentos do tutor: um por pagamento, um por cupom e mais um por
    /// pagamento com valor estornado.
    /// </summary>
    private static IEnumerable<LancamentoDoHistoricoDto> Lancamentos(
        IEnumerable<Pagamento> pagamentos, IEnumerable<CupomResgate> cupons)
    {
        foreach (var p in pagamentos)
        {
            yield return new LancamentoDoHistoricoDto
            {
                Tipo = TipoDeLancamento.Pagamento,
                Momento = p.Momento,
                Descricao = $"{DescricaoDo(p.Tipo)} - {p.MeioPagamento}",
                ValorEmReais = p.ValorCobrado,
                Status = p.StatusPagamento.ToString(),
                PagamentoId = p.Id,
                ConsultaId = p.ConsultaId,
                InternacaoId = p.InternacaoId
            };

            if (p.ValorEstornado is > 0m and var estornado)
            {
                yield return new LancamentoDoHistoricoDto
                {
                    Tipo = TipoDeLancamento.Reembolso,

                    // Estorno anterior a Sprint 4 nao tem data propria: cai na do pagamento
                    Momento = p.EstornadoEm ?? p.Momento,
                    Descricao = $"Reembolso de {DescricaoDo(p.Tipo).ToLowerInvariant()}",
                    ValorEmReais = estornado,
                    Status = p.StatusPagamento.ToString(),
                    PagamentoId = p.Id,
                    ConsultaId = p.ConsultaId,
                    InternacaoId = p.InternacaoId
                };
            }
        }

        foreach (var c in cupons)
        {
            yield return new LancamentoDoHistoricoDto
            {
                Tipo = TipoDeLancamento.Resgate,
                Momento = c.EmitidoEm,
                Descricao = $"Resgate: {c.ItemNome ?? c.ItemRef}",
                ValorEmReais = c.Desconto,
                Pontos = c.PontosDebitados,
                Status = c.Status.ToString(),
                CupomId = c.Id,
                ItemMarketplaceId = c.ItemMarketplaceId
            };
        }
    }

    private static string DescricaoDo(TipoPagamento tipo) => tipo switch
    {
        TipoPagamento.Caucao => "Caucao de internacao",
        TipoPagamento.SaldoInternacao => "Saldo de internacao",
        _ => "Consulta"
    };

    private static bool Atende(LancamentoDoHistoricoDto lancamento, FiltroHistoricoDaCarteiraDto filtro) =>
        (filtro.Tipo is null || lancamento.Tipo == filtro.Tipo)
        && (filtro.De is null || lancamento.Momento >= filtro.De)
        && (filtro.Ate is null || lancamento.Momento <= filtro.Ate)
        && (string.IsNullOrWhiteSpace(filtro.Status)
            || string.Equals(lancamento.Status, filtro.Status.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Totais sobre os lançamentos que passaram no filtro.
    ///
    /// Pago conta confirmado, parcial e estornado: o dinheiro saiu nos três, e o que
    /// voltou aparece em <c>TotalEstornado</c>. Contar só os confirmados faria um
    /// pagamento reembolsado sumir do "pago" e ficar só no "estornado" — o
    /// Responsável veria que recebeu de volta algo que, pela tela, nunca pagou.
    /// </summary>
    private static ResumoDaCarteiraDto Resumir(
        IReadOnlyCollection<LancamentoDoHistoricoDto> filtrados, IEnumerable<Pagamento> pagamentos)
    {
        var cobrados = filtrados
            .Where(l => l.Tipo == TipoDeLancamento.Pagamento && SaiuDoBolso(l.Status))
            .ToList();

        // O desconto mora no pagamento, nao no lancamento: cruza pelos ids filtrados
        var idsCobrados = cobrados.Select(l => l.PagamentoId).ToHashSet();
        var descontos = pagamentos
            .Where(p => idsCobrados.Contains(p.Id))
            .Sum(p => p.ValorDoDesconto ?? 0m);

        return new ResumoDaCarteiraDto
        {
            TotalPago = cobrados.Sum(l => l.ValorEmReais ?? 0m),
            TotalEstornado = filtrados.Where(l => l.Tipo == TipoDeLancamento.Reembolso).Sum(l => l.ValorEmReais ?? 0m),
            TotalDeDescontos = descontos,
            PontosResgatados = filtrados.Where(l => l.Tipo == TipoDeLancamento.Resgate).Sum(l => l.Pontos ?? 0),
            Liquidacao = "Simulada"
        };
    }

    private static bool SaiuDoBolso(string status) =>
        status == nameof(StatusPagamento.Confirmado)
        || status == nameof(StatusPagamento.Parcial)
        || status == nameof(StatusPagamento.Estornado);
}
