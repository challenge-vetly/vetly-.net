using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Programa de fidelidade: pontos por consulta realizada e desconto no resgate
/// (RN-051/RN-052).
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
    private readonly IUsuarioAtual _usuario;

    public FidelidadeService(
        IFidelidadeRepository repo,
        IConsultaRepository consultaRepo,
        IPagamentoRepository pagamentoRepo,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _pagamentoRepo = pagamentoRepo;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<SaldoDePontosDto> ObterSaldoAsync(Guid tutorId)
    {
        GarantirEscopo(tutorId);

        var movimentos = (await _repo.ObterDoTutorAsync(tutorId)).ToList();

        var saldo = movimentos.Sum(m => m.Pontos);

        // O que vence nos próximos 30 dias, para o Responsável usar antes de perder
        var limite = DateTime.UtcNow.AddDays(30);

        var vencendo = movimentos
            .Where(m => m.Tipo == TipoMovimentoDePontos.Credito
                        && m.ExpiraEm is { } expira && expira <= limite && expira > DateTime.UtcNow)
            .Sum(m => m.Pontos);

        return new SaldoDePontosDto
        {
            TutorId = tutorId,
            Saldo = saldo,
            ValorEmReais = MovimentoDePontos.EmReais(saldo),
            PontosVencendoEm30Dias = Math.Min(vencendo, Math.Max(saldo, 0)),
            MinimoParaResgate = MovimentoDePontos.MinimoParaResgate,
            PodeResgatar = saldo >= MovimentoDePontos.MinimoParaResgate
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<MovimentoDePontosDto>> ObterExtratoAsync(Guid tutorId)
    {
        GarantirEscopo(tutorId);

        var movimentos = await _repo.ObterDoTutorAsync(tutorId);

        return movimentos
            .OrderByDescending(m => m.OcorridoEm)
            .Select(m => new MovimentoDePontosDto
            {
                Id = m.Id,
                Tipo = m.Tipo,
                Pontos = m.Pontos,
                ConsultaId = m.ConsultaId,
                PagamentoId = m.PagamentoId,
                ValorEmReais = m.ValorEmReais,
                ExpiraEm = m.ExpiraEm,
                Descricao = m.Descricao,
                OcorridoEm = m.OcorridoEm
            });
    }

    /// <inheritdoc/>
    public async Task<MovimentoDePontosDto?> CreditarPorConsultaAsync(Guid consultaId)
    {
        // Job reentregue não credita duas vezes pelo mesmo atendimento
        if (await _repo.ObterCreditoDaConsultaAsync(consultaId) is not null)
            return null;

        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        // RN-052: o crédito é pelo atendimento que aconteceu. Consulta cancelada ou
        // expirada não gera ponto, senão o programa pagaria por receita que não entrou.
        if (consulta.Status != StatusConsulta.Realizada)
            return null;

        var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(consultaId);

        if (pagamento is null || pagamento.StatusPagamento != StatusPagamento.Confirmado || pagamento.Valor <= 0)
            return null;

        var movimento = MovimentoDePontos.PorConsulta(consulta.TutorId, consultaId, pagamento.Valor);

        await _repo.AdicionarAsync(movimento);
        await _repo.SalvarAsync();

        return new MovimentoDePontosDto
        {
            Id = movimento.Id,
            Tipo = movimento.Tipo,
            Pontos = movimento.Pontos,
            ConsultaId = movimento.ConsultaId,
            ExpiraEm = movimento.ExpiraEm,
            Descricao = movimento.Descricao,
            OcorridoEm = movimento.OcorridoEm
        };
    }

    /// <inheritdoc/>
    public async Task<DescontoAplicadoDto> ApurarDescontoAsync(
        Guid tutorId, int pontos, decimal valorDaCobranca, decimal teto)
    {
        if (pontos < MovimentoDePontos.MinimoParaResgate)
            throw new BusinessRuleException("RN-051",
                $"O resgate minimo e de {MovimentoDePontos.MinimoParaResgate} pontos.");

        var saldo = (await _repo.ObterDoTutorAsync(tutorId)).Sum(m => m.Pontos);

        if (pontos > saldo)
            throw new BusinessRuleException("RN-051",
                $"Saldo insuficiente: {saldo} ponto(s) disponivel(is).");

        var desconto = MovimentoDePontos.EmReais(pontos);

        // O desconto sai da comissao da plataforma, e nao pode passar dela: a Vetly
        // banca a propria fidelidade, mas nao paga para atender. O prestador recebe o
        // repasse cheio nos dois casos (RN-051/RN-072).
        var maximo = Math.Min(teto, valorDaCobranca);

        if (desconto > maximo)
        {
            var pontosPermitidos = (int)Math.Floor(maximo / MovimentoDePontos.ReaisPorPonto);

            throw new BusinessRuleException("RN-051",
                $"O desconto excede o limite desta cobranca. Resgate no maximo {pontosPermitidos} ponto(s).");
        }

        return new DescontoAplicadoDto
        {
            PontosResgatados = pontos,
            ValorDoDesconto = desconto,
            ValorFinal = valorDaCobranca - desconto
        };
    }

    /// <inheritdoc/>
    public async Task RegistrarResgateAsync(Guid tutorId, int pontos, decimal valorEmReais, Guid pagamentoId)
    {
        var movimento = MovimentoDePontos.PorResgate(tutorId, pontos, valorEmReais, pagamentoId);

        await _repo.AdicionarAsync(movimento);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<int> ExpirarVencidosAsync()
    {
        var vencidos = (await _repo.ObterCreditosVencidosSemBaixaAsync(DateTime.UtcNow)).ToList();

        if (vencidos.Count == 0)
            return 0;

        var expirados = 0;

        // A expiração é por Responsável: o extrato mostra um lançamento de baixa, e não
        // o saldo caindo sozinho sem explicação.
        foreach (var porTutor in vencidos.GroupBy(m => m.TutorId))
        {
            var saldo = (await _repo.ObterDoTutorAsync(porTutor.Key)).Sum(m => m.Pontos);

            // Ponto já gasto não expira de novo. Sem isso, quem resgatou tudo e depois
            // viu o crédito vencer ficaria com saldo negativo — devendo pontos que já
            // usou legitimamente.
            var restante = Math.Max(saldo, 0);

            foreach (var credito in porTutor.OrderBy(m => m.ExpiraEm))
            {
                if (restante <= 0)
                    break;

                var aExpirar = Math.Min(credito.Pontos, restante);

                await _repo.AdicionarAsync(
                    MovimentoDePontos.PorExpiracao(porTutor.Key, aExpirar, credito.Id));

                restante -= aExpirar;
                expirados += aExpirar;
            }
        }

        await _repo.SalvarAsync();

        return expirados;
    }

    /// <summary>Os pontos são do Responsável: o escopo vem do token (RN-105/RN-106).</summary>
    private void GarantirEscopo(Guid tutorId)
    {
        if (_usuario.EhAdmin || _usuario.TutorId == tutorId)
            return;

        throw new AcessoNegadoException("RN-106", "Estes pontos nao pertencem ao seu escopo de acesso.");
    }
}
