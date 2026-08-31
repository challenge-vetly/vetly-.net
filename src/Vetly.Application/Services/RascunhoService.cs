using System.Text;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Estruturação da consulta pela IA (RN-080, §7.3).
///
/// A IA lê a transcrição e devolve prontuário estruturado. O que ela produz é
/// sugestão até o veterinário decidir (RN-082) — e o rascunho guarda o texto de
/// origem justamente para que essa decisão seja informada.
/// </summary>
public class RascunhoService : IRascunhoService
{
    private readonly ICapturaRepository _repo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IOllamaService _ia;
    private readonly IUsuarioAtual _usuario;

    public RascunhoService(
        ICapturaRepository repo,
        IConsultaRepository consultaRepo,
        IAnimalRepository animalRepo,
        IOllamaService ia,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _animalRepo = animalRepo;
        _ia = ia;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task GerarAsync(Guid sessaoCapturaId)
    {
        var sessao = await _repo.ObterSessaoAsync(sessaoCapturaId)
            ?? throw new NotFoundException("Sessao de captura", sessaoCapturaId);

        // Job reentregue não gera um segundo rascunho sobre o mesmo atendimento
        if (await _repo.ObterRascunhoDaSessaoAsync(sessaoCapturaId) is not null)
            return;

        if (sessao.Estado is not (EstadoSessaoCaptura.GerandoRascunho or EstadoSessaoCaptura.TranscricaoParcial))
            return;

        var parcial = sessao.Estado == EstadoSessaoCaptura.TranscricaoParcial;

        sessao.IniciarEstruturacao();
        _repo.AtualizarSessao(sessao);
        await _repo.SalvarAsync();

        var transcricao = await MontarTranscricaoAsync(sessaoCapturaId);

        if (string.IsNullOrWhiteSpace(transcricao))
        {
            // Nada transcrito: não há o que estruturar, e oferecer um rascunho vazio
            // seria pior que assumir o caminho manual (RN-085)
            Desistir(sessao);
            await _repo.SalvarAsync();
            return;
        }

        var consulta = await _consultaRepo.ObterPorIdAsync(sessao.ConsultaId)
            ?? throw new NotFoundException("Consulta", sessao.ConsultaId);

        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId);

        var contexto = MontarContexto(transcricao, animal, parcial);

        var comecou = DateTime.UtcNow;
        ConsultaEstruturadaDto estruturada;

        try
        {
            estruturada = await _ia.EstruturarConsultaAsync(contexto);
        }
        catch (Exception)
        {
            // IA fora do ar não pode travar a consulta: o atendimento aconteceu e
            // precisa virar prontuário de algum jeito (RN-085). O veterinário
            // preenche à mão.
            Desistir(sessao);
            await _repo.SalvarAsync();
            throw;
        }

        var duracao = (int)(DateTime.UtcNow - comecou).TotalMilliseconds;

        var rascunho = new RascunhoIa(
            sessao.Id,
            sessao.ConsultaId,
            estruturada.Anamnese,
            estruturada.ExameFisico,
            estruturada.HipotesesDiagnosticas,
            estruturada.Conduta,
            estruturada.Orientacoes,
            transcricao,
            Modelo,
            parcial,
            MontarAvisos(parcial, animal),
            duracao);

        if (rascunho.EstaVazio())
        {
            Desistir(sessao);
            await _repo.SalvarAsync();
            return;
        }

        await _repo.AdicionarRascunhoAsync(rascunho);

        sessao.RascunhoDisponivel();
        _repo.AtualizarSessao(sessao);

        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<RascunhoIaDto> ObterDaConsultaAsync(Guid consultaId)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        // RN-105: o rascunho é conteúdo clínico do atendimento de quem o conduziu
        if (!_usuario.EhAdmin && _usuario.VeterinarioId != consulta.VeterinarioId)
            throw new AcessoNegadoException("RN-105", "Esta consulta nao pertence ao seu escopo de acesso.");

        var rascunho = await _repo.ObterRascunhoDaConsultaAsync(consultaId)
            ?? throw new NotFoundException("Rascunho da consulta", consultaId);

        var sessao = await _repo.ObterSessaoDaConsultaAsync(consultaId);

        return new RascunhoIaDto
        {
            Id = rascunho.Id,
            ConsultaId = rascunho.ConsultaId,
            EstadoDaSessao = sessao?.Estado ?? EstadoSessaoCaptura.RascunhoPronto,
            Anamnese = rascunho.Anamnese,
            ExameFisico = rascunho.ExameFisico,
            HipotesesDiagnosticas = [.. rascunho.HipotesesDiagnosticas],
            Conduta = rascunho.Conduta,
            Orientacoes = rascunho.Orientacoes,
            TextoOrigem = rascunho.TextoOrigem,
            Modelo = rascunho.Modelo,
            Parcial = rascunho.Parcial,
            Avisos = [.. rascunho.Avisos],
            GeradoEm = rascunho.GeradoEm,
            DuracaoMs = rascunho.DuracaoMs
        };
    }

    /// <summary>Identificação do que produziu o rascunho, para a trilha de auditoria.</summary>
    private const string Modelo = "ollama/llama3.1";

    /// <summary>
    /// A estruturação não deu certo. Cai no caminho manual em vez de deixar a consulta
    /// presa num estado intermediário (RN-085).
    /// </summary>
    private void Desistir(SessaoCaptura sessao)
    {
        sessao.EstruturacaoFalhou();
        _repo.AtualizarSessao(sessao);
    }

    /// <summary>Junta o texto dos segmentos na ordem em que foram falados.</summary>
    private async Task<string> MontarTranscricaoAsync(Guid sessaoId)
    {
        var segmentos = (await _repo.ObterSegmentosAsync(sessaoId)).OrderBy(s => s.Sequencia);
        var porSegmento = (await _repo.ObterTranscricoesAsync(sessaoId)).ToDictionary(t => t.SegmentoAudioId);

        var texto = new StringBuilder();

        foreach (var segmento in segmentos)
        {
            if (porSegmento.TryGetValue(segmento.Id, out var transcricao))
                texto.AppendLine(transcricao.Texto);
        }

        return texto.ToString().Trim();
    }

    /// <summary>
    /// Dados do animal entram no contexto porque mudam a leitura clínica do que foi
    /// dito. Animal não encontrado não impede a estruturação — o texto é o essencial.
    /// </summary>
    private static ContextoDaEstruturacaoDto MontarContexto(string transcricao, Animal? animal, bool parcial) => new()
    {
        Transcricao = transcricao,
        Especie = animal?.Especie ?? string.Empty,
        Raca = animal?.Raca ?? string.Empty,
        IdadeAnos = animal is null ? 0 : Math.Max(0, (int)((DateTime.UtcNow - animal.DataNascimento).TotalDays / 365.25)),
        PesoKg = animal?.PesoKg,
        Sexo = animal?.Sexo?.ToString(),
        Alergias = animal is null ? [] : [.. animal.Alergias],
        CondicoesPreexistentes = animal is null ? [] : [.. animal.CondicoesPreexistentes],
        TranscricaoParcial = parcial
    };

    /// <summary>
    /// Avisos que acompanham o rascunho. São o que o veterinário precisa saber antes
    /// de aprovar: que falta áudio, ou que sem peso não haverá dose (RN-081).
    /// </summary>
    private static List<string> MontarAvisos(bool parcial, Animal? animal)
    {
        var avisos = new List<string>();

        if (parcial)
            avisos.Add("TranscricaoParcial");

        if (animal?.PesoKg is null or <= 0)
            avisos.Add("PesoAusente");

        return avisos;
    }
}
