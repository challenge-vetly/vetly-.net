using Vetly.Application.DTOs.Analytics;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Métricas agregadas da plataforma (RN-106).
///
/// São três perguntas: o agendamento está virando atendimento? o dinheiro está
/// entrando? a IA está ajudando ou dando trabalho?
///
/// Nenhum número identifica pessoa. Analytics é agregado, e cruzar métrica com dado
/// de Responsável ou de animal seria usar a base clínica para outra coisa.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IConsultaRepository _consultaRepo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IAuditoriaIaRepository _auditoria;
    private readonly IUsuarioAtual _usuario;

    public AnalyticsService(
        IConsultaRepository consultaRepo,
        IPagamentoRepository pagamentoRepo,
        IAuditoriaIaRepository auditoria,
        IUsuarioAtual usuario)
    {
        _consultaRepo = consultaRepo;
        _pagamentoRepo = pagamentoRepo;
        _auditoria = auditoria;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<AnalyticsDaPlataformaDto> ObterDaPlataformaAsync(DateTime? inicio, DateTime? fim)
    {
        if (!_usuario.EhAdmin)
            throw new AcessoNegadoException("RN-106",
                "As metricas da plataforma sao restritas a administracao.");

        var agora = DateTime.UtcNow;

        // Período padrão: os últimos 30 dias. É a janela em que uma métrica ainda
        // reage ao que foi mudado — trimestre inteiro esconde a semana ruim.
        var ate = fim ?? agora;
        var de = inicio ?? ate.AddDays(-30);

        if (de > ate)
            throw new ValidationException("inicio", "O inicio do periodo nao pode ser depois do fim.");

        var consultas = (await _consultaRepo.ObterNoPeriodoAsync(de, ate)).ToList();
        var pagamentos = (await _pagamentoRepo.ObterConfirmadosNoPeriodoAsync(de, ate)).ToList();
        var decisoes = (await _auditoria.ObterNoPeriodoAsync(de, ate)).ToList();

        return new AnalyticsDaPlataformaDto
        {
            PeriodoInicio = de,
            PeriodoFim = ate,
            Funil = MontarFunil(consultas),
            Ia = MontarUsoDaIa(decisoes),
            Receita = MontarReceita(pagamentos)
        };
    }

    /// <summary>
    /// O caminho do agendamento até o atendimento. As taxas importam mais que os
    /// absolutos: 30 cancelamentos em 1000 consultas é ruído; em 60, é problema.
    /// </summary>
    private static FunilDeAtendimentoDto MontarFunil(List<Consulta> consultas)
    {
        var funil = new FunilDeAtendimentoDto
        {
            Criadas = consultas.Count,
            Realizadas = consultas.Count(c => c.Status == StatusConsulta.Realizada),
            Canceladas = consultas.Count(c => c.Status == StatusConsulta.Cancelada),
            NoShow = consultas.Count(c => c.Status == StatusConsulta.NoShow),
            Expiradas = consultas.Count(c => c.Status == StatusConsulta.Expirada)
        };

        // "Confirmada" aqui é quem passou do pagamento em algum momento, e não quem
        // está confirmada agora: uma consulta já realizada passou por lá.
        funil.Confirmadas = consultas.Count(c => c.StatusPagamento == StatusPagamento.Confirmado);

        funil.TaxaDeConversao = Percentual(funil.Realizadas, funil.Criadas);
        funil.TaxaDeCancelamento = Percentual(funil.Canceladas, funil.Confirmadas);
        funil.TaxaDeNoShow = Percentual(funil.NoShow, funil.Confirmadas);

        return funil;
    }

    /// <summary>
    /// Como a IA está sendo recebida (RN-082).
    ///
    /// A métrica que interessa não é quantos rascunhos foram gerados: é quantos o
    /// veterinário aceitou sem corrigir. Correção alta significa que a IA está dando
    /// trabalho em vez de poupar.
    /// </summary>
    private static UsoDaIaDto MontarUsoDaIa(List<LogAuditoriaIa> decisoes)
    {
        var sobreRascunho = decisoes
            .Where(d => d.Decisao != DecisaoSobreRascunho.Manual)
            .ToList();

        var ia = new UsoDaIaDto
        {
            DecisoesRegistradas = decisoes.Count,
            Aprovados = decisoes.Count(d => d.Decisao == DecisaoSobreRascunho.Aprovado),
            Corrigidos = decisoes.Count(d => d.Decisao == DecisaoSobreRascunho.Corrigido),
            NaoAprovados = decisoes.Count(d => d.Decisao == DecisaoSobreRascunho.NaoAprovado),
            ProntuariosManuais = decisoes.Count(d => d.Decisao == DecisaoSobreRascunho.Manual)
        };

        // O denominador é só o que passou pela IA: prontuário manual não é um rascunho
        // recusado, é um atendimento que nunca teve rascunho.
        ia.TaxaDeAprovacaoSemCorrecao = Percentual(ia.Aprovados, sobreRascunho.Count);
        ia.TaxaDeRecusa = Percentual(ia.NaoAprovados, sobreRascunho.Count);

        return ia;
    }

    /// <summary>Volume e valor do que foi cobrado (RN-070).</summary>
    private static ReceitaDoPeriodoDto MontarReceita(List<Pagamento> pagamentos)
    {
        var bruto = pagamentos.Sum(p => p.Valor);
        var comissao = pagamentos.Sum(p => p.Comissao ?? 0m);

        return new ReceitaDoPeriodoDto
        {
            TransacoesConfirmadas = pagamentos.Count,
            ValorBruto = bruto,
            ComissaoDaPlataforma = comissao,
            TicketMedio = pagamentos.Count == 0 ? 0m : Math.Round(bruto / pagamentos.Count, 2),

            // Efetivo, e não nominal: o desconto de fidelidade sai da comissão, então o
            // que a plataforma retém de fato é menor que o take rate do plano (RN-051).
            TakeRateEfetivo = Percentual(comissao, bruto)
        };
    }

    /// <summary>
    /// Percentual de 0 a 100, com duas casas. Denominador zero devolve zero em vez de
    /// estourar: período sem movimento é situação normal, não erro.
    /// </summary>
    private static decimal Percentual(decimal parte, decimal total) =>
        total == 0m ? 0m : Math.Round(parte / total * 100m, 2);
}
