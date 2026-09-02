using System.Security.Cryptography;
using System.Text;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Captura de áudio da consulta (RN-008/RN-009/RN-079/RN-085).
///
/// A janela é aberta e fechada pelo veterinário. Fora dela nada é capturado — é um
/// limite explícito de gravação, e não uma escolha de implementação.
/// </summary>
public class CapturaService : ICapturaService
{
    /// <summary>
    /// Quanto se espera pelo callback antes de dar o segmento como travado (§4.2).
    ///
    /// Três minutos para um trecho de ~30s é folga larga de propósito: o prazo existe
    /// para pegar o motor que morreu calado, não para competir com o motor lento.
    /// </summary>
    public static readonly TimeSpan PrazoDoCallback = TimeSpan.FromMinutes(3);

    private readonly ICapturaRepository _repo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IEmpresaRepository _empresaRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IMidiaRepository _midiaRepo;
    private readonly IFilaDeJobs _fila;
    private readonly IUsuarioAtual _usuario;

    public CapturaService(
        ICapturaRepository repo,
        IConsultaRepository consultaRepo,
        IVeterinarioRepository vetRepo,
        IEmpresaRepository empresaRepo,
        IAnimalRepository animalRepo,
        IMidiaRepository midiaRepo,
        IFilaDeJobs fila,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _vetRepo = vetRepo;
        _empresaRepo = empresaRepo;
        _animalRepo = animalRepo;
        _midiaRepo = midiaRepo;
        _fila = fila;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<SessaoIniciadaDto> IniciarAsync(Guid consultaId)
    {
        var consulta = await ObterConsultaDoVeterinarioAsync(consultaId);

        if (consulta.Status != StatusConsulta.Confirmada)
            throw new BusinessRuleException("RN-008",
                "So e possivel iniciar uma consulta confirmada.");

        var existente = await _repo.ObterSessaoDaConsultaAsync(consultaId);
        if (existente is not null)
            throw new ConflitoDeEstadoException("RN-008", "Esta consulta ja foi iniciada.");

        // RN-085: a IA na consulta e exclusiva dos planos Profissional e Enterprise.
        // No Basico a consulta inicia normalmente — o que nao existe e a captura.
        var capturaAtiva = await PlanoTemCapturaAsync(consulta.VeterinarioId, consulta.EmpresaId);

        var sessao = new SessaoCaptura(consultaId, capturaAtiva);
        await _repo.AdicionarSessaoAsync(sessao);

        consulta.RegistrarInicio(sessao.IniciadaEm);
        _consultaRepo.Atualizar(consulta);

        await _repo.SalvarAsync();
        await _consultaRepo.SalvarAsync();

        return new SessaoIniciadaDto
        {
            SessaoCapturaId = sessao.Id,
            CapturaAtiva = sessao.CapturaAtiva,
            IniciadaEm = sessao.IniciadaEm,
            Gravacao = capturaAtiva ? new ParametrosDeGravacaoDto() : null,
            Avisos = await MontarAvisosAsync(consulta, capturaAtiva)
        };
    }

    /// <inheritdoc/>
    public async Task<SegmentoRecebidoDto> ReceberSegmentoAsync(Guid consultaId, EnviarSegmentoDto dto)
    {
        await ObterConsultaDoVeterinarioAsync(consultaId);

        var sessao = await _repo.ObterSessaoDaConsultaAsync(consultaId)
            ?? throw new BusinessRuleException("RN-008", "A consulta ainda nao foi iniciada.");

        // RN-079: fora da janela nao se captura audio. Vale tanto para a consulta ja
        // encerrada quanto para o plano que nao tem captura.
        if (!sessao.JanelaAberta())
            throw new ConflitoDeEstadoException("RN-079",
                "A janela de captura desta consulta nao esta aberta.");

        var midia = await _midiaRepo.ObterPorIdAsync(dto.MidiaId)
            ?? throw new NotFoundException("Midia", dto.MidiaId);

        if (midia.Tipo != TipoMidia.AudioConsulta)
            throw new ValidationException("midiaId", "A midia informada nao e um audio de consulta.");

        var jaExiste = await _repo.ObterSegmentoPorSequenciaAsync(sessao.Id, dto.Sequencia);
        if (jaExiste is not null)
            throw new ConflitoDeEstadoException("RN-009",
                $"O trecho {dto.Sequencia} desta consulta ja foi recebido.");

        var segmento = new SegmentoAudio(sessao.Id, dto.Sequencia, dto.MidiaId, dto.DuracaoMs, dto.InicioRelativoMs);

        await _repo.AdicionarSegmentoAsync(segmento);
        await _repo.SalvarAsync();

        // O despacho ao motor acontece fora da requisicao: o veterinario nao pode ficar
        // esperando a transcricao para continuar o atendimento (§4.2).
        await _fila.EnfileirarAsync(TipoJob.TranscreverSegmento, segmento.Id.ToString());

        return new SegmentoRecebidoDto
        {
            SegmentoId = segmento.Id,
            Sequencia = segmento.Sequencia,
            Estado = segmento.Estado
        };
    }

    /// <inheritdoc/>
    public async Task<EstadoDaCapturaDto> ObterEstadoAsync(Guid consultaId)
    {
        await ObterConsultaDoVeterinarioAsync(consultaId);

        var sessao = await _repo.ObterSessaoDaConsultaAsync(consultaId)
            ?? throw new NotFoundException("Sessao de captura", consultaId);

        var segmentos = (await _repo.ObterSegmentosAsync(sessao.Id)).OrderBy(s => s.Sequencia).ToList();
        var transcricoes = await _repo.ObterTranscricoesAsync(sessao.Id);

        var porSegmento = transcricoes.ToDictionary(t => t.SegmentoAudioId);

        var texto = new StringBuilder();
        foreach (var segmento in segmentos)
        {
            if (porSegmento.TryGetValue(segmento.Id, out var transcricao))
                texto.AppendLine(transcricao.Texto);
        }

        return new EstadoDaCapturaDto
        {
            SessaoCapturaId = sessao.Id,
            Estado = sessao.Estado,
            CapturaAtiva = sessao.CapturaAtiva,
            IniciadaEm = sessao.IniciadaEm,
            EncerradaEm = sessao.EncerradaEm,
            SegmentosRecebidos = segmentos.Count,
            SegmentosTranscritos = segmentos.Count(s => s.Estado == EstadoSegmentoAudio.Transcrito),
            SegmentosComFalha = segmentos.Count(s => s.Estado == EstadoSegmentoAudio.Falha),
            TextoParcial = texto.ToString().TrimEnd(),
            Segmentos = [.. segmentos.Select(s => new SegmentoDaCapturaDto
            {
                Id = s.Id,
                Sequencia = s.Sequencia,
                Estado = s.Estado,
                FalhaMotivo = s.FalhaMotivo,
                Tentativas = s.Tentativas
            })]
        };
    }

    /// <inheritdoc/>
    public async Task<ConsultaEncerradaDto> EncerrarAsync(Guid consultaId)
    {
        var consulta = await ObterConsultaDoVeterinarioAsync(consultaId);

        var sessao = await _repo.ObterSessaoDaConsultaAsync(consultaId)
            ?? throw new BusinessRuleException("RN-008", "A consulta ainda nao foi iniciada.");

        if (sessao.EncerradaEm is not null)
            throw new ConflitoDeEstadoException("RN-008", "Esta consulta ja foi encerrada.");

        var segmentos = (await _repo.ObterSegmentosAsync(sessao.Id)).ToList();

        sessao.Encerrar(segmentos.Count);
        _repo.AtualizarSessao(sessao);

        // RN-038: encerrar e o que marca a consulta como realizada, e e o que dispara
        // a avaliacao (RN-055) e a pontuacao (RN-052).
        //
        // Realizar e nao Finalizar: o atendimento acabou, mas o prontuario ainda nao
        // foi gerado nem a receita assinada. Marcar Finalizada aqui daria por fechada
        // uma documentacao que sequer comecou, e a RN-087 nunca seria cobrada.
        consulta.Realizar();
        consulta.RegistrarEncerramento(sessao.EncerradaEm!.Value);
        _consultaRepo.Atualizar(consulta);

        await _repo.SalvarAsync();
        await _consultaRepo.SalvarAsync();

        // RN-052: o atendimento aconteceu, entao os pontos sao devidos. Sai da
        // requisicao porque o veterinario nao pode esperar o programa de fidelidade
        // para fechar a consulta, e uma falha aqui nao pode desfazer o encerramento.
        await _fila.EnfileirarAsync(TipoJob.CreditarPontosDaConsulta, consulta.Id.ToString());

        // Se todos os segmentos ja tiveram desfecho, o ciclo pode seguir agora
        await AvaliarDesfechoDaTranscricaoAsync(sessao.Id);

        return new ConsultaEncerradaDto
        {
            ConsultaId = consulta.Id,
            StatusConsulta = consulta.Status,
            EstadoDaSessao = sessao.Estado,
            EncerradaEm = sessao.EncerradaEm!.Value,
            SegmentosPendentes = segmentos.Count(s => !s.TemDesfecho())
        };
    }

    /// <inheritdoc/>
    public async Task RegistrarCallbackAsync(CallbackDeTranscricaoDto dto)
    {
        var segmento = await _repo.ObterSegmentoAsync(dto.SegmentoId)
            ?? throw new NotFoundException("Segmento de audio", dto.SegmentoId);

        GarantirTokenDoSegmento(segmento, dto.CallbackToken);

        // Callback e entregue mais de uma vez por natureza: segmento com desfecho
        // ignora a reentrega, sem duplicar texto nem reabrir o ciclo.
        if (segmento.TemDesfecho())
            return;

        if (dto.Status.Equals("Ok", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(dto.Texto))
        {
            var transcricao = new Transcricao(
                segmento.Id, dto.Texto, dto.Confianca, dto.Trechos,
                dto.Motor is null ? null : $"{dto.Motor.Nome} {dto.Motor.Versao}".Trim());

            await _repo.AdicionarTranscricaoAsync(transcricao);
            segmento.RegistrarTranscricao();
        }
        else
        {
            segmento.RegistrarFalha(dto.Motivo ?? MotivoFalhaTranscricao.AudioIlegivel);

            // Ainda ha tentativa: o segmento volta para a fila
            if (segmento.Estado == EstadoSegmentoAudio.Recebido)
            {
                await _fila.EnfileirarAsync(
                    TipoJob.TranscreverSegmento, segmento.Id.ToString(), BackoffDaTentativa(segmento.Tentativas));
            }
        }

        _repo.AtualizarSegmento(segmento);
        await _repo.SalvarAsync();

        await AvaliarDesfechoDaTranscricaoAsync(segmento.SessaoCapturaId);
    }

    /// <inheritdoc/>
    public async Task<int> ResolverSegmentosTravadosAsync()
    {
        var agora = DateTime.UtcNow;
        var travados = (await _repo.ObterSegmentosComEsperaEsgotadaAsync(agora - PrazoDoCallback)).ToList();

        if (travados.Count == 0)
            return 0;

        // As sessoes tocadas sao avaliadas depois de gravar, e nao dentro do laco: o
        // desfecho da sessao depende de TODOS os segmentos dela, e avaliar a cada
        // trecho leria um estado que ainda esta pela metade.
        var sessoes = new HashSet<Guid>();
        var tratados = 0;

        foreach (var segmento in travados)
        {
            // Reconfere no proprio agregado: entre a consulta e este ponto o callback
            // pode ter chegado, e reenviar um trecho ja transcrito duplicaria o texto.
            if (!segmento.EsperaEsgotada(PrazoDoCallback, agora))
                continue;

            // Timeout e nao AudioIlegivel: o audio pode estar perfeito — quem nao
            // respondeu foi o motor, e o motivo e o que o veterinario le no aviso.
            // A contagem da tentativa fica no agregado, que sabe se o despacho chegou
            // a acontecer (§4.2).
            segmento.EncerrarEsperaVencida();
            _repo.AtualizarSegmento(segmento);

            if (segmento.Estado == EstadoSegmentoAudio.Recebido)
            {
                await _fila.EnfileirarAsync(
                    TipoJob.TranscreverSegmento, segmento.Id.ToString(), BackoffDaTentativa(segmento.Tentativas));
            }

            sessoes.Add(segmento.SessaoCapturaId);
            tratados++;
        }

        if (tratados == 0)
            return 0;

        await _repo.SalvarAsync();

        // Destravada a espera, a sessao pode finalmente progredir a TranscricaoParcial
        // ou SemTranscricao — que e o estado terminal que o app fica esperando.
        foreach (var sessaoId in sessoes)
            await AvaliarDesfechoDaTranscricaoAsync(sessaoId);

        return tratados;
    }

    /// <summary>
    /// Espera antes de tentar de novo, por número de tentativas já feitas (§4.2).
    ///
    /// Crescente e não fixa: a primeira falha costuma ser transitória e 10s bastam;
    /// insistir no mesmo intervalo contra um motor que caiu de vez só consome o worker
    /// e adia o desfecho, que é o que o veterinário está esperando.
    /// </summary>
    public static TimeSpan BackoffDaTentativa(int tentativas) => tentativas switch
    {
        <= 1 => TimeSpan.FromSeconds(10),
        2 => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromSeconds(90)
    };

    /// <summary>
    /// Confere que o callback é a resposta <b>deste</b> segmento (RN-009).
    ///
    /// O token de serviço no cabeçalho autentica o fluxo de transcrição como um todo;
    /// só ele deixaria quem o conhecesse escrever texto no prontuário de qualquer
    /// consulta, bastando acertar um id de segmento. O token por segmento fecha isso:
    /// ele é sorteado no despacho, só o hash fica na base, e o motor tem de devolvê-lo.
    ///
    /// A comparação é em tempo fixo: comparar hash com <c>==</c> vaza, pelo tempo de
    /// resposta, quantos caracteres iniciais estavam certos — e um atacante que pode
    /// repetir a chamada transforma isso em adivinhação caractere a caractere.
    ///
    /// Segmento sem hash gravado é segmento que ainda não foi despachado: não há
    /// callback legítimo possível para ele.
    /// </summary>
    private static void GarantirTokenDoSegmento(SegmentoAudio segmento, string? token)
    {
        if (string.IsNullOrWhiteSpace(segmento.CallbackTokenHash) || string.IsNullOrWhiteSpace(token))
            throw new AcessoNegadoException("RN-009", "Callback de transcricao sem token valido.");

        var recebido = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        var esperado = segmento.CallbackTokenHash;

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(recebido), Encoding.UTF8.GetBytes(esperado)))
        {
            throw new AcessoNegadoException("RN-009", "Callback de transcricao sem token valido.");
        }
    }

    /// <summary>
    /// Quando todos os segmentos têm desfecho e a janela já fechou, decide se o ciclo
    /// segue para a estruturação, segue parcial ou cai no caminho manual (§7.3).
    /// </summary>
    private async Task AvaliarDesfechoDaTranscricaoAsync(Guid sessaoId)
    {
        var sessao = await _repo.ObterSessaoAsync(sessaoId);

        if (sessao is null || sessao.Estado != EstadoSessaoCaptura.AguardandoTranscricao)
            return;

        var segmentos = (await _repo.ObterSegmentosAsync(sessaoId)).ToList();

        if (segmentos.Any(s => !s.TemDesfecho()))
            return;

        sessao.RegistrarDesfechoDaTranscricao(
            segmentos.Count(s => s.Estado == EstadoSegmentoAudio.Transcrito),
            segmentos.Count(s => s.Estado == EstadoSegmentoAudio.Falha));

        _repo.AtualizarSessao(sessao);
        await _repo.SalvarAsync();

        // Ha texto: a estruturacao segue fora da requisicao (RN-080). Transcricao
        // parcial tambem segue — o rascunho sai com o que ha, e com aviso.
        if (sessao.Estado is EstadoSessaoCaptura.GerandoRascunho or EstadoSessaoCaptura.TranscricaoParcial)
            await _fila.EnfileirarAsync(TipoJob.EstruturarConsulta, sessao.Id.ToString());
    }

    /// <summary>
    /// A consulta pertence ao veterinário que a está conduzindo (RN-105). O Admin da
    /// unidade também alcança.
    /// </summary>
    private async Task<Consulta> ObterConsultaDoVeterinarioAsync(Guid consultaId)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (_usuario.EhAdmin || _usuario.VeterinarioId == consulta.VeterinarioId)
            return consulta;

        throw new AcessoNegadoException("RN-105", "Esta consulta nao pertence ao seu escopo de acesso.");
    }

    /// <summary>
    /// A captura existe nos planos Profissional e Enterprise (RN-085). Vet vinculado
    /// herda o plano da clínica, que é quem assina.
    /// </summary>
    private async Task<bool> PlanoTemCapturaAsync(Guid veterinarioId, Guid? empresaId)
    {
        if (empresaId is { } id)
        {
            var empresa = await _empresaRepo.ObterPorIdAsync(id);
            if (empresa is not null)
                return empresa.Plano != PlanoAssinatura.Basico;
        }

        var vet = await _vetRepo.ObterPorIdAsync(veterinarioId);

        return vet is not null && vet.Plano != PlanoAssinatura.Basico;
    }

    /// <summary>
    /// Avisos que o veterinário precisa ver antes de começar. O peso ausente é o mais
    /// importante: sem ele a IA não sugere dose (RN-081), e descobrir isso no fim do
    /// atendimento seria tarde.
    /// </summary>
    private async Task<List<string>> MontarAvisosAsync(Consulta consulta, bool capturaAtiva)
    {
        var avisos = new List<string>();

        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId);

        if (animal?.PesoKg is null or <= 0)
            avisos.Add("PesoAusente");

        if (!capturaAtiva)
            avisos.Add("CapturaIndisponivelNoPlanoBasico");

        return avisos;
    }
}
