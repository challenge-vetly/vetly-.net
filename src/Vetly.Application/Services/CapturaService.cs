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
        // a avaliacao (RN-055) e a pontuacao (RN-052) nas ondas seguintes.
        consulta.Finalizar();
        consulta.RegistrarEncerramento(sessao.EncerradaEm!.Value);
        _consultaRepo.Atualizar(consulta);

        await _repo.SalvarAsync();
        await _consultaRepo.SalvarAsync();

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
                await _fila.EnfileirarAsync(TipoJob.TranscreverSegmento, segmento.Id.ToString(), TimeSpan.FromSeconds(30));
        }

        _repo.AtualizarSegmento(segmento);
        await _repo.SalvarAsync();

        await AvaliarDesfechoDaTranscricaoAsync(segmento.SessaoCapturaId);
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
