using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.API.Filters;
using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.DTOs.Redistribuicao;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Controller de consultas.
/// Agendamento requer pagamento confirmado (RN-006).
/// Cancelamento aplica Strategy por antecedencia (RN-014/RN-041/RN-042).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConsultasController : ControllerBase
{
    private readonly IConsultaService _service;
    private readonly ICapturaService _captura;
    private readonly IRascunhoService _rascunhos;
    private readonly IProntuarioService _prontuarios;
    private readonly IRedistribuicaoService _redistribuicao;

    public ConsultasController(
        IConsultaService service,
        ICapturaService captura,
        IRascunhoService rascunhos,
        IProntuarioService prontuarios,
        IRedistribuicaoService redistribuicao)
    {
        _service = service;
        _captura = captura;
        _rascunhos = rascunhos;
        _prontuarios = prontuarios;
        _redistribuicao = redistribuicao;
    }

    /// <summary>
    /// Lista consultas com filtros opcionais, paginada (§2.3).
    /// A resposta e o envelope { itens, total, pagina, tamanho }; sem paginacao
    /// informada valem pagina 1 e 20 itens, com teto de 100 por pagina.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ResultadoPaginado<ConsultaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObterTodas(
        [FromQuery] FiltroConsultaDto filtro,
        [FromQuery] Paginacao paginacao) =>
        Ok(await _service.ObterTodosAsync(filtro, paginacao));

    /// <summary>
    /// Trava o horario por 10 minutos e cria a consulta em EmCheckout (RN-003/RN-035).
    /// O agendamento so se confirma com o pagamento (RN-006).
    /// </summary>
    /// <remarks>
    /// Convive com <c>POST /api/consultas</c>, que segue sendo o caminho da emergencia
    /// presencial e do balcao, onde o pagamento e no ato (RN-040).
    ///
    /// Horario ja reservado por outra pessoa devolve 409: e so escolher outro.
    /// </remarks>
    [HttpPost("checkout")]
    [Idempotente]
    [ProducesResponseType(typeof(CheckoutCriadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> IniciarCheckout([FromBody] CheckoutDto dto)
    {
        var checkout = await _service.IniciarCheckoutAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = checkout.ConsultaId }, checkout);
    }

    /// <summary>Retorna uma consulta pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Retorna todas as consultas de um veterinario, opcionalmente filtradas por data.</summary>
    [HttpGet("veterinario/{veterinarioId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ConsultaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorVeterinario(Guid veterinarioId) =>
        Ok(await _service.ObterPorVeterinarioAsync(veterinarioId));

    /// <summary>Retorna todas as consultas de um animal.</summary>
    [HttpGet("animal/{animalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ConsultaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorAnimal(Guid animalId) =>
        Ok(await _service.ObterPorAnimalAsync(animalId));

    /// <summary>Agenda uma consulta (RN-006: pagamento deve estar confirmado).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Agendar([FromBody] CriarConsultaDto dto)
    {
        var criada = await _service.AgendarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    /// <summary>Cancela uma consulta aplicando a Strategy de reembolso (RN-014/RN-041/RN-042).</summary>
    [HttpDelete("{id:guid}")]
    [Idempotente]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var resultado = await _service.CancelarAsync(id);
        return Ok(resultado);
    }

    /// <summary>Finaliza a consulta — exige receita veterinaria assinada digitalmente (RN-087).</summary>
    [HttpPost("{id:guid}/finalizar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Finalizar(Guid id)
    {
        await _service.FinalizarAsync(id);
        return NoContent();
    }

    // ── Agendamento: pre-sintomas, simulacao, remarcacao e no-show ───────────

    /// <summary>
    /// Registra os pre-sintomas informados pelo Responsavel (RN-005/RN-036).
    /// </summary>
    /// <remarks>
    /// E texto guiado, e nao campo livre: perguntas fechadas produzem contexto que o
    /// veterinario le em dez segundos no briefing e que a IA consegue usar. Um
    /// paragrafo solto nao faz nem uma coisa nem outra.
    ///
    /// So vale antes do atendimento: depois, o briefing ja foi lido e a IA ja recebeu
    /// o contexto — informar ali nao alimenta nada.
    ///
    /// As midias ja devem estar no storage; aqui viajam apenas os `midiaIds` (§2.6).
    /// </remarks>
    [HttpPut("{id:guid}/pre-sintomas")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarPreSintomas(Guid id, [FromBody] PreSintomasDto dto)
    {
        await _service.RegistrarPreSintomasAsync(id, dto);

        return NoContent();
    }

    /// <summary>
    /// Mostra o que aconteceria se a consulta fosse cancelada agora
    /// (RN-014/RN-041/RN-042).
    /// </summary>
    /// <remarks>
    /// Nao executa nada: e a mesma selecao de Strategy do cancelamento, aplicada so
    /// para calcular. Se a simulacao usasse outro criterio, ela mostraria um valor e o
    /// cancelamento cobraria outro — que e exatamente o que a RN-042 quer evitar ao
    /// exigir que a politica de retencao seja transparente no agendamento.
    ///
    /// <c>liquidacao</c> vem sempre <c>Simulada</c>: no MVP o valor e calculado e
    /// exibido, nao devolvido (RN-041).
    /// </remarks>
    [HttpGet("{id:guid}/simulacao-cancelamento")]
    [ProducesResponseType(typeof(SimulacaoDeCancelamentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SimularCancelamento(Guid id) =>
        Ok(await _service.SimularCancelamentoAsync(id));

    /// <summary>
    /// Transfere a consulta para outro horario, sem nova cobranca (RN-013/RN-043).
    /// </summary>
    /// <remarks>
    /// O pagamento ja realizado acompanha a nova data (RN-013). O limite e de <b>2
    /// remarcacoes por consulta</b>: duas cobrem imprevisto legitimo, e acima disso
    /// remarcar vira burla a janela de reembolso — quem quer desistir sem perder
    /// dinheiro empurraria a data indefinidamente em vez de cancelar sob a politica.
    /// Esgotado o limite, devolve 422 e resta cancelar.
    ///
    /// A remarcacao mantem o mesmo veterinario; trocar de profissional e cancelar e
    /// agendar de novo, ou redistribuir (RN-025).
    ///
    /// O horario novo e travado antes de mover a consulta, e o antigo volta a fila de
    /// espera (RN-035/RN-037).
    /// </remarks>
    [HttpPost("{id:guid}/remarcar")]
    [Idempotente]
    [ProducesResponseType(typeof(RemarcacaoRealizadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Remarcar(Guid id, [FromBody] RemarcarConsultaDto dto) =>
        Ok(await _service.RemarcarAsync(id, dto));

    /// <summary>Registra o nao comparecimento do Responsavel (RN-038/RN-044).</summary>
    /// <remarks>
    /// Sem reembolso, seguindo a faixa "menos de 2h ou no ato" da RN-014: nao ha
    /// penalidade nova, so a politica que ja existia.
    ///
    /// Quem registra e quem estava esperando — o profissional ou a unidade. O
    /// Responsavel nao declara o proprio no-show.
    /// </remarks>
    [HttpPost("{id:guid}/no-show")]
    [ProducesResponseType(typeof(NoShowRegistradoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarNoShow(Guid id) =>
        Ok(await _service.RegistrarNoShowAsync(id));

    /// <summary>Retorna briefing pre-consulta com animal, historico e exames recentes.</summary>
    [HttpGet("{id:guid}/briefing")]
    [ProducesResponseType(typeof(BriefingConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterBriefing(Guid id) =>
        Ok(await _service.ObterBriefingAsync(id));

    /// <summary>
    /// Decisao do veterinario sobre o rascunho da IA (RN-082).
    /// </summary>
    /// <remarks>
    /// Sao tres caminhos, e a escolha e explicita — nao ha aprovacao por omissao:
    ///
    /// - <c>Aprovado</c>: o rascunho vira conteudo clinico como veio;
    /// - <c>Corrigido</c>: exige o conteudo corrigido, porque corrigir sem dizer o que
    ///   mudou nao e corrigir;
    /// - <c>NaoAprovado</c>: exige justificativa, o ciclo encerra sem documentos e o
    ///   diagnostico NAO fica validado — sem validacao nao se gera documento.
    ///
    /// Toda decisao vira registro append-only na trilha de auditoria: e o que sustenta
    /// a afirmacao de que nenhuma sugestao chegou ao prontuario sem decisao humana.
    /// </remarks>
    [HttpPut("{id:guid}/validar-diagnostico")]
    [ProducesResponseType(typeof(DecisaoRegistradaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ValidarDiagnostico(Guid id, [FromBody] DecisaoDoProntuarioDto dto) =>
        Ok(await _prontuarios.DecidirAsync(id, dto));

    /// <summary>
    /// Prontuario escrito a mao pelo veterinario, sem IA no caminho (RN-085).
    /// </summary>
    /// <remarks>
    /// E o caminho quando nao houve captura: plano Basico, falha da transcricao ou
    /// rascunho recusado. O atendimento aconteceu e precisa virar prontuario de algum
    /// jeito.
    ///
    /// Havendo rascunho ainda pendente, devolve 409: a decisao sobre ele vem primeiro,
    /// senao ficariam dois prontuarios concorrentes sobre o mesmo atendimento.
    /// </remarks>
    [HttpPost("{id:guid}/prontuario-manual")]
    [ProducesResponseType(typeof(DecisaoRegistradaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarProntuarioManual(
        Guid id, [FromBody] ProntuarioManualDto dto) =>
        Ok(await _prontuarios.RegistrarManualAsync(id, dto));

    /// <summary>
    /// Trilha de auditoria das decisoes sobre conteudo de IA da consulta (RN-082).
    /// </summary>
    /// <remarks>
    /// Append-only: os registros nao sao alterados nem removidos. A mesma consulta
    /// pode acumular decisoes — a recusa do rascunho e, depois, o prontuario manual
    /// que a sucede.
    /// </remarks>
    [HttpGet("{id:guid}/auditoria-ia")]
    [ProducesResponseType(typeof(List<LogAuditoriaIaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterAuditoriaIa(Guid id) =>
        Ok(await _prontuarios.ObterAuditoriaAsync(id));

    // ── Redistribuicao (RN-025) ──────────────────────────────────────────────

    /// <summary>
    /// Veterinarios que poderiam assumir esta consulta (RN-025).
    /// </summary>
    /// <remarks>
    /// A ordem e pela proximidade do horario original, e nao por reputacao: quem
    /// agendou as 14h de terca organizou o dia em torno disso, e trocar o profissional
    /// ja e uma quebra — trocar tambem o horario e outra.
    ///
    /// Especie e eliminatoria (RN-029): encaminhar um felino para quem so atende caes
    /// nao e uma sugestao pior, e uma sugestao errada. So entram veterinarios ativos,
    /// publicados e da mesma UF.
    /// </remarks>
    [HttpGet("{id:guid}/redistribuicao/candidatos")]
    [ProducesResponseType(typeof(IEnumerable<CandidatoARedistribuicaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SugerirCandidatos(Guid id) =>
        Ok(await _redistribuicao.SugerirCandidatosAsync(id));

    /// <summary>
    /// Passa a consulta a outro veterinario, mantendo pagamento e animal (RN-025).
    /// </summary>
    /// <remarks>
    /// E o caminho para quando o profissional sai da plataforma ou fica indisponivel.
    /// Cancelar em massa jogaria o problema no colo do Responsavel, que agendou de
    /// boa-fe e teria de refazer tudo — inclusive pagar de novo.
    ///
    /// O horario novo e travado antes de mover a consulta: sem isso, duas
    /// redistribuicoes simultaneas mandariam dois animais para o mesmo slot. O horario
    /// antigo volta a disponibilidade.
    ///
    /// O Responsavel e avisado, e o <c>motivo</c> e obrigatorio porque entra na
    /// mensagem: aviso sem motivo soa como erro do app (RN-092).
    ///
    /// Restrito a administracao: nem o veterinario que sai decide para quem vai.
    /// </remarks>
    [HttpPost("{id:guid}/redistribuir")]
    [ProducesResponseType(typeof(RedistribuicaoRealizadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Redistribuir(Guid id, [FromBody] RedistribuirConsultaDto dto) =>
        Ok(await _redistribuicao.RedistribuirAsync(id, dto));

    // ── Captura de audio da consulta (RN-008/RN-009/RN-079/RN-085) ───────────

    /// <summary>
    /// Abre a janela de captura: a consulta comeca aqui (RN-008).
    /// </summary>
    /// <remarks>
    /// No plano Basico a consulta inicia normalmente, mas SEM captura (RN-085) — o
    /// prontuario e preenchido manualmente. A resposta traz avisos que o veterinario
    /// precisa ver antes de comecar, como peso ausente, que impede sugestao de dose
    /// (RN-081).
    /// </remarks>
    [HttpPost("{id:guid}/iniciar")]
    [ProducesResponseType(typeof(SessaoIniciadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Iniciar(Guid id) =>
        Ok(await _captura.IniciarAsync(id));

    /// <summary>
    /// Recebe um trecho de audio da consulta e enfileira a transcricao (RN-009).
    /// </summary>
    /// <remarks>
    /// Fora da janela de captura devolve 409: a IA nao captura audio nem produz
    /// conteudo clinico fora dela (RN-079). O audio ja deve estar no storage — aqui
    /// viaja apenas o midiaId.
    /// </remarks>
    [HttpPost("{id:guid}/captura/segmentos")]
    [ProducesResponseType(typeof(SegmentoRecebidoDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReceberSegmento(Guid id, [FromBody] EnviarSegmentoDto dto) =>
        Accepted(await _captura.ReceberSegmentoAsync(id, dto));

    /// <summary>
    /// Situacao da captura, com o texto ja transcrito ate agora (RN-009).
    /// </summary>
    [HttpGet("{id:guid}/captura")]
    [ProducesResponseType(typeof(EstadoDaCapturaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterCaptura(Guid id) =>
        Ok(await _captura.ObterEstadoAsync(id));

    /// <summary>
    /// Fecha a janela de captura e marca a consulta como realizada (RN-008/RN-038).
    /// </summary>
    /// <remarks>
    /// A partir daqui os processos pos-consulta sao disparados. Segmento que ainda nao
    /// voltou do motor continua sendo aguardado — a consulta nao espera por ele.
    /// </remarks>
    [HttpPost("{id:guid}/encerrar")]
    [ProducesResponseType(typeof(ConsultaEncerradaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Encerrar(Guid id) =>
        Ok(await _captura.EncerrarAsync(id));

    /// <summary>
    /// Rascunho de prontuario estruturado pela IA a partir da consulta (RN-080).
    /// </summary>
    /// <remarks>
    /// E rascunho, e a palavra e literal: nada aqui vira documento sem a decisao
    /// explicita do veterinario (RN-082). A resposta traz a transcricao de origem
    /// junto, para que a decisao seja informada — e os avisos que pesam nela, como
    /// <c>TranscricaoParcial</c> e <c>PesoAusente</c> (RN-081).
    ///
    /// 404 enquanto a estruturacao nao terminou, ou quando ela nao produziu nada e a
    /// consulta seguiu pelo prontuario manual (RN-085) — <c>GET
    /// /api/consultas/{id}/captura</c> mostra em que ponto do ciclo a sessao esta.
    /// </remarks>
    [HttpPost("{id:guid}/retorno")]
    [Authorize(Policy = "VeterinarioOuAdmin")]
    [Idempotente]
    [ProducesResponseType(typeof(RetornoAgendadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AgendarRetorno(Guid id, [FromBody] AgendarRetornoDto dto)
    {
        var retorno = await _service.AgendarRetornoAsync(id, dto);

        return CreatedAtAction(nameof(ObterPorId), new { id = retorno.ConsultaId }, retorno);
    }

    [HttpGet("{id:guid}/rascunho")]
    [ProducesResponseType(typeof(RascunhoIaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterRascunho(Guid id) =>
        Ok(await _rascunhos.ObterDaConsultaAsync(id));
}
