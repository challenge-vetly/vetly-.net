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
    private readonly ILembreteRepository _lembreteRepo;
    private readonly IEmpresaRepository _empresaRepo;
    private readonly IAgendaRepository _agendaRepo;
    private readonly IUsuarioAtual _usuario;

    /// <summary>
    /// Janela de alertas da régua exibida no painel (RN-095, §6.4).
    ///
    /// Noventa dias: um cuidado que ficou sem resposta há mais de um trimestre não é
    /// mais um caso para ligar hoje, é histórico — e deixá-lo na lista faria o painel
    /// crescer para sempre até ninguém mais olhar para ele.
    /// </summary>
    private static readonly TimeSpan JanelaDeAlertasDaRegua = TimeSpan.FromDays(90);

    public DashboardService(
        IConsultaRepository consultaRepo,
        IVeterinarioRepository vetRepo,
        IAnimalRepository animalRepo,
        IPagamentoRepository pagamentoRepo,
        IDocumentoRepository documentoRepo,
        ICapturaRepository capturaRepo,
        IAvaliacaoRepository avaliacaoRepo,
        ILembreteRepository lembreteRepo,
        IEmpresaRepository empresaRepo,
        IAgendaRepository agendaRepo,
        IUsuarioAtual usuario)
    {
        _consultaRepo = consultaRepo;
        _vetRepo = vetRepo;
        _animalRepo = animalRepo;
        _pagamentoRepo = pagamentoRepo;
        _documentoRepo = documentoRepo;
        _capturaRepo = capturaRepo;
        _avaliacaoRepo = avaliacaoRepo;
        _lembreteRepo = lembreteRepo;
        _empresaRepo = empresaRepo;
        _agendaRepo = agendaRepo;
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
            NotaPublica = vet.TemNotaPublica(),

            // §6.4: o alerta da régua era gravado e não chegava a lugar nenhum.
            // Restrito aos animais que este profissional atendeu — a régua é do
            // animal, mas quem tem contexto para ligar é quem já o viu.
            ResponsaveisNaoResponsivos = await MontarAlertasDaReguaAsync([vetId])
        };
    }

/// <inheritdoc/>
    public async Task<DashboardDaUnidadeDto> ObterDaUnidadeAsync(DateTime? data)
    {
        var empresa = await UnidadeDoTokenAsync();
        var referencia = (data ?? DateTime.UtcNow).Date;

        var inicioDoDia = referencia;
        var fimDoDia = referencia.AddDays(1).AddTicks(-1);

        var vinculados = (await _vetRepo.ObterPorEmpresaAsync(empresa.Id))
            .OrderBy(v => v.Nome)
            .ToList();

        var painel = new DashboardDaUnidadeDto
        {
            EmpresaId = empresa.Id,
            Nome = empresa.Nome,
            Data = referencia
        };

        var indicadores = new IndicadoresDaUnidadeDto
        {
            ProfissionaisAtivos = vinculados.Count(v => v.Ativo)
        };

        foreach (var vet in vinculados)
        {
            var consultas = (await _consultaRepo.ObterPorVeterinarioAsync(vet.Id, inicioDoDia, fimDoDia))
                .ToList();

            var slots = (await _agendaRepo.ObterSlotsAsync(vet.Id, inicioDoDia, fimDoDia)).ToList();
            var ocupados = slots.Count(s => s.Estado != EstadoSlot.Livre);

            painel.Profissionais.Add(new AgendaDoProfissionalDto
            {
                VeterinarioId = vet.Id,
                Nome = vet.Nome,
                Ativo = vet.Ativo,
                HorariosNoDia = slots.Count,
                HorariosOcupados = ocupados,
                AgendaDeHoje = await MontarAgendaAsync(consultas)
            });

            // Mesma regra do painel do profissional: cancelada e expirada não são
            // atendimento, e contá-las como "do dia" inflaria a ocupação da unidade.
            indicadores.AtendimentosNoDia += consultas.Count(c =>
                c.Status is not (StatusConsulta.Cancelada or StatusConsulta.Expirada));

            indicadores.Realizados += consultas.Count(c => c.Status == StatusConsulta.Realizada);
            indicadores.Cancelados += consultas.Count(c => c.Status == StatusConsulta.Cancelada);
            indicadores.NoShow += consultas.Count(c => c.Status == StatusConsulta.NoShow);

            indicadores.HorariosNoDia += slots.Count;
            indicadores.HorariosOcupados += ocupados;
        }

        // Unidade sem agenda materializada fica em 0%, e não em 100%: uma divisão por
        // zero mal tratada faria a clínica que nem configurou agenda aparecer lotada.
        indicadores.TaxaDeOcupacao = indicadores.HorariosNoDia == 0
            ? 0m
            : Math.Round(100m * indicadores.HorariosOcupados / indicadores.HorariosNoDia, 1);

        painel.Indicadores = indicadores;
        painel.ResponsaveisNaoResponsivos = await MontarAlertasDaReguaAsync(
            [.. vinculados.Select(v => v.Id)]);

        return painel;
    }

    /// <summary>
    /// A unidade que o administrador da requisição administra (§7.3, RN-106).
    ///
    /// A empresa vem do vínculo do próprio Admin, e não de um id que o cliente
    /// escolhe: com id na rota, qualquer Admin leria o painel de qualquer clínica, que
    /// é o "dados de outros estabelecimentos" que a §7.3 veda em letra.
    ///
    /// Admin que administra mais de uma unidade recebe a primeira por ordem de nome —
    /// o cadastro do MVP é de um administrador por unidade, e escolher entre várias é
    /// uma tela que ainda não existe.
    /// </summary>
    private async Task<Empresa> UnidadeDoTokenAsync()
    {
        if (!_usuario.EhAdmin)
            throw new AcessoNegadoException("RN-106",
                "O painel da unidade e da administracao do estabelecimento.");

        var vetId = _usuario.VeterinarioId
            ?? throw new AcessoNegadoException("RN-106",
                "O painel da unidade exige um administrador vinculado a um cadastro profissional.");

        var administradas = (await _empresaRepo.ObterPorAdministradorAsync(vetId))
            .OrderBy(e => e.Nome)
            .ToList();

        return administradas.FirstOrDefault()
            ?? throw new NotFoundException("Empresa administrada por", vetId);
    }

    /// <summary>
    /// Réguas que esgotaram as tentativas sem resposta, filtradas aos animais que os
    /// profissionais informados atenderam (RN-095, §6.4).
    ///
    /// O filtro por animal atendido é o que impede o alerta de virar uma lista da
    /// plataforma inteira: a régua nasce do calendário do animal e não guarda vet
    /// nenhum, então quem "responde" por ela é quem o atendeu. Sem esse recorte, cada
    /// clínica veria os Responsáveis omissos de todas as outras.
    /// </summary>
    private async Task<List<AlertaDeReguaDto>> MontarAlertasDaReguaAsync(Guid[] veterinarioIds)
    {
        var agora = DateTime.UtcNow;
        var escalados = (await _lembreteRepo.ObterEscaladosParaClinicaAsync(
            agora.Subtract(JanelaDeAlertasDaRegua))).ToList();

        if (escalados.Count == 0)
            return [];

        // O cruzamento vai para o banco levando os DOIS conjuntos, e ambos sao pequenos:
        // os animais saem das reguas que esgotaram tres tentativas, e os profissionais,
        // de uma unidade. Carregar as consultas de cada vet para cruzar aqui leria o
        // historico inteiro da clinica para responder sobre meia duzia de animais.
        var atendidos = await _consultaRepo.ObterAnimaisAtendidosAsync(
            veterinarioIds, escalados.Select(l => l.AnimalId));

        var alertas = new List<AlertaDeReguaDto>();

        foreach (var lembrete in escalados.Where(l => atendidos.Contains(l.AnimalId)))
        {
            var animal = await _animalRepo.ObterPorIdAsync(lembrete.AnimalId);

            alertas.Add(new AlertaDeReguaDto
            {
                LembreteId = lembrete.Id,
                AnimalId = lembrete.AnimalId,
                AnimalNome = animal?.Nome ?? "Animal nao encontrado",
                TutorId = lembrete.TutorId,
                Tipo = lembrete.Tipo,
                DataEvento = lembrete.DataEvento,
                TentativasRealizadas = lembrete.TentativasRealizadas
            });
        }

        return alertas;
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
