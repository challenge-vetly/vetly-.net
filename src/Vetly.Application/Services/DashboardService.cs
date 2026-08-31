using Vetly.Application.DTOs.Dashboard;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Painel do veterinário (RN-105).
///
/// Não é relatório: é o que precisa da atenção dele agora. A ordem das seções segue
/// a ordem em que as coisas travam — pendência de documentação bloqueia pagamento,
/// agenda define o dia, números do mês são contexto.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly IConsultaRepository _consultaRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly IDocumentoRepository _documentoRepo;
    private readonly ICapturaRepository _capturaRepo;
    private readonly IAvaliacaoRepository _avaliacaoRepo;
    private readonly IUsuarioAtual _usuario;

    public DashboardService(
        IConsultaRepository consultaRepo,
        IVeterinarioRepository vetRepo,
        IAnimalRepository animalRepo,
        IPagamentoRepository pagamentoRepo,
        IDocumentoRepository documentoRepo,
        ICapturaRepository capturaRepo,
        IAvaliacaoRepository avaliacaoRepo,
        IUsuarioAtual usuario)
    {
        _consultaRepo = consultaRepo;
        _vetRepo = vetRepo;
        _animalRepo = animalRepo;
        _pagamentoRepo = pagamentoRepo;
        _documentoRepo = documentoRepo;
        _capturaRepo = capturaRepo;
        _avaliacaoRepo = avaliacaoRepo;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<DashboardDoVeterinarioDto> ObterDoVeterinarioAsync(DateTime? data)
    {
        // O painel é do próprio profissional: não há id na rota, e o escopo vem do
        // token (RN-105). Nem o Admin pede o painel de outro por aqui.
        var vetId = _usuario.VeterinarioId
            ?? throw new AcessoNegadoException("RN-105",
                "O painel e do proprio veterinario. Entre com um cadastro de veterinario.");

        var vet = await _vetRepo.ObterPorIdAsync(vetId)
            ?? throw new NotFoundException("Veterinario", vetId);

        var referencia = (data ?? DateTime.UtcNow).Date;

        // O dia é o dia de calendário UTC, como no resto do sistema
        var consultasDoDia = await _consultaRepo.ObterPorVeterinarioAsync(
            vetId, referencia, referencia.AddDays(1).AddTicks(-1));

        var inicioDoMes = new DateTime(referencia.Year, referencia.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var fimDoMes = inicioDoMes.AddMonths(1).AddTicks(-1);

        var consultasDoMes = (await _consultaRepo.ObterPorVeterinarioAsync(vetId, inicioDoMes, fimDoMes)).ToList();

        return new DashboardDoVeterinarioDto
        {
            VeterinarioId = vetId,
            Nome = vet.Nome,
            Data = referencia,
            AgendaDeHoje = await MontarAgendaAsync(consultasDoDia),
            Pendencias = await MontarPendenciasAsync(vetId, consultasDoMes),
            Mes = await MontarResumoDoMesAsync(inicioDoMes, fimDoMes, consultasDoMes),
            NotaMedia = vet.NotaMedia,
            NumAvaliacoes = vet.NumAvaliacoes,
            NotaPublica = vet.TemNotaPublica()
        };
    }

    /// <summary>
    /// A agenda do dia, do próximo atendimento ao último. Consulta cancelada some: o
    /// painel serve para conduzir o dia, e horário cancelado não é atendimento.
    /// </summary>
    private async Task<List<AtendimentoDoDiaDto>> MontarAgendaAsync(IEnumerable<Consulta> consultas)
    {
        var agenda = new List<AtendimentoDoDiaDto>();

        foreach (var consulta in consultas.Where(c => c.Status != StatusConsulta.Cancelada
                                                      && c.Status != StatusConsulta.Expirada)
                                          .OrderBy(c => c.DataHora))
        {
            var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId);

            agenda.Add(new AtendimentoDoDiaDto
            {
                ConsultaId = consulta.Id,
                DataHora = consulta.DataHora,
                AnimalId = consulta.AnimalId,
                AnimalNome = animal?.Nome ?? "Animal nao encontrado",
                Especie = animal?.Especie ?? string.Empty,
                Status = consulta.Status,
                Modalidade = consulta.Modalidade,

                // Descobrir que falta peso durante a consulta é tarde (RN-081)
                PesoAusente = animal?.PesoKg is null or <= 0
            });
        }

        return agenda;
    }

    /// <summary>
    /// O que está parado esperando ação do veterinário. São as coisas que travam
    /// dinheiro ou documento — não uma lista de tudo que existe.
    /// </summary>
    private async Task<PendenciasDoVeterinarioDto> MontarPendenciasAsync(
        Guid vetId, List<Consulta> consultasDoMes)
    {
        var naoEncerradas = 0;
        var rascunhosPendentes = 0;
        var documentosPendentes = 0;

        foreach (var consulta in consultasDoMes)
        {
            var sessao = await _capturaRepo.ObterSessaoDaConsultaAsync(consulta.Id);

            // Iniciada e nunca encerrada: a consulta não gera nada enquanto isso
            if (sessao is not null && sessao.EncerradaEm is null)
                naoEncerradas++;

            var rascunho = await _capturaRepo.ObterRascunhoDaConsultaAsync(consulta.Id);

            if (rascunho is not null && rascunho.AguardandoDecisao())
                rascunhosPendentes++;

            var documentos = await _documentoRepo.ObterPorConsultaAsync(consulta.Id);
            documentosPendentes += documentos.Count(d => d.PendenteDeAssinatura());
        }

        var avaliacoes = await _avaliacaoRepo.ObterDoVeterinarioAsync(vetId);
        var semResposta = avaliacoes.Count(a => a.RespondidaEm is null);

        return new PendenciasDoVeterinarioDto
        {
            ConsultasNaoEncerradas = naoEncerradas,
            RascunhosAguardandoDecisao = rascunhosPendentes,
            DocumentosAguardandoAssinatura = documentosPendentes,
            AvaliacoesSemResposta = semResposta,
            TemPendencia = naoEncerradas + rascunhosPendentes + documentosPendentes > 0
        };
    }

    /// <summary>
    /// Números do mês. Só o que foi efetivamente cobrado soma: consulta cancelada
    /// aparece na contagem de cancelamentos, mas não em dinheiro que não existiu.
    /// </summary>
    private async Task<ResumoDoMesDto> MontarResumoDoMesAsync(
        DateTime inicio, DateTime fim, List<Consulta> consultas)
    {
        var resumo = new ResumoDoMesDto
        {
            Inicio = inicio,
            Fim = fim,
            Cancelamentos = consultas.Count(c => c.Status == StatusConsulta.Cancelada)
        };

        foreach (var consulta in consultas)
        {
            var pagamento = await _pagamentoRepo.ObterPorConsultaAsync(consulta.Id);

            if (pagamento is null || pagamento.StatusPagamento != StatusPagamento.Confirmado)
                continue;

            resumo.AtendimentosRealizados++;
            resumo.ValorBruto += pagamento.Valor;
            resumo.RepasseApurado += pagamento.Repasse ?? 0m;

            if (!pagamento.Liquidado)
                resumo.RepassePendente += pagamento.Repasse ?? 0m;
        }

        return resumo;
    }
}
