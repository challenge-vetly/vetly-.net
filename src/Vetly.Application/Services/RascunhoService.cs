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
    private readonly IColmeiaService _colmeia;

    public RascunhoService(
        ICapturaRepository repo,
        IConsultaRepository consultaRepo,
        IAnimalRepository animalRepo,
        IOllamaService ia,
        IUsuarioAtual usuario,
        IColmeiaService colmeia)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _animalRepo = animalRepo;
        _ia = ia;
        _usuario = usuario;
        _colmeia = colmeia;
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

        var contexto = await MontarContextoAsync(transcricao, consulta, animal, parcial);

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
    private async Task<ContextoDaEstruturacaoDto> MontarContextoAsync(
        string transcricao, Consulta consulta, Animal? animal, bool parcial)
    {
        var contexto = new ContextoDaEstruturacaoDto
        {
            Transcricao = transcricao,
            Especie = animal?.Especie ?? string.Empty,
            Raca = animal?.Raca ?? string.Empty,
            IdadeAnos = animal is null ? 0 : Math.Max(0, (int)((DateTime.UtcNow - animal.DataNascimento).TotalDays / 365.25)),
            PesoKg = animal?.PesoKg,
            Sexo = animal?.Sexo?.ToString(),
            Alergias = animal is null ? [] : [.. animal.Alergias],
            CondicoesPreexistentes = animal is null ? [] : [.. animal.CondicoesPreexistentes],

            // RN-068: alerta de seguranca nao e ocultavel e nao depende de colmeia.
            // Ele entra no contexto mesmo quando o historico nao entra — e justamente
            // o dado cuja ausencia pode custar caro numa prescricao.
            AlertasAtivos = animal is null ? [] : [.. animal.AlertasAtivos],

            // RN-005/RN-036: o relato de quem convive com o animal. Frequentemente traz
            // o que a consulta nao repete em voz alta.
            PreSintomas = consulta.PreSintomas,

            TranscricaoParcial = parcial
        };

        if (animal is not null)
            contexto.HistoricoRelevante = [.. await HistoricoNoEscopoAsync(consulta, animal)];

        return contexto;
    }

    /// <summary>
    /// Resumo dos atendimentos anteriores que a IA pode considerar, pelo mesmo filtro
    /// de colmeia da leitura humana (RN-064/RN-066).
    ///
    /// Uma IA que lesse o histórico inteiro quando o profissional não pode lê-lo seria
    /// uma forma indireta de contornar o consentimento: o texto voltaria ao vet dentro
    /// do rascunho, sem nunca ter passado pela guarda. O filtro aqui é o mesmo do
    /// briefing — e o acesso também vira log (RN-067), porque quem lê em nome do
    /// veterinário continua sendo o veterinário.
    /// </summary>
    private async Task<IEnumerable<string>> HistoricoNoEscopoAsync(Consulta consulta, Animal animal)
    {
        var vetId = consulta.VeterinarioId;

        var temColmeia = await _colmeia.PodeAcessarAsync(
            vetId, animal.Id, EscopoAcessoColmeia.HistoricoCompleto);

        // O ator vai explicito porque este metodo roda dentro de um job: nao ha
        // requisicao HTTP, e o IUsuarioAtual que a sobrecarga curta consulta traria
        // VeterinarioId nulo. O log sairia sem quem leu — e ele e exatamente o registro
        // que a RN-067 torna visivel ao Responsavel. Quem le em nome do veterinario
        // continua sendo o veterinario, mesmo quando quem executa a leitura e a IA.
        await _colmeia.RegistrarAcessoAsync(
            vetId, animal.Id, EscopoAcessoColmeia.HistoricoCompleto, temColmeia,
            "IA: estruturacao do prontuario");

        // O historico clinico vive no prontuario; a consulta diz apenas quem o
        // produziu, que e o que o filtro de colmeia precisa saber.
        var consultas = (await _consultaRepo.ObterPorAnimalAsync(animal.Id))
            .ToDictionary(c => c.Id, c => c);

        var prontuarios = (await _animalRepo.ObterHistoricoLongitudinalAsync(animal.Id))
            .Where(p => p.ConsultaId != consulta.Id);

        if (!temColmeia)
        {
            prontuarios = prontuarios.Where(p =>
                consultas.TryGetValue(p.ConsultaId, out var c) && c.VeterinarioId == vetId);
        }

        // Tres atendimentos bastam: contexto demais dilui a transcricao, que e o que a
        // IA tem de estruturar de fato.
        return prontuarios
            .OrderByDescending(p => p.DataCriacao)
            .Take(3)
            .Select(p => $"{p.DataCriacao:dd/MM/yyyy}: {Resumir(p.DadosClinicos)}");
    }

    /// <summary>
    /// Corta o prontuario anterior no tamanho de um lembrete.
    ///
    /// O historico entra como contexto, nao como leitura: mandar prontuarios inteiros
    /// afogaria a transcricao, que e o que a IA tem de estruturar de fato.
    /// </summary>
    private static string Resumir(string dadosClinicos)
    {
        const int Limite = 300;

        var texto = dadosClinicos.ReplaceLineEndings(" ").Trim();

        return texto.Length <= Limite ? texto : texto[..Limite] + "...";
    }

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
