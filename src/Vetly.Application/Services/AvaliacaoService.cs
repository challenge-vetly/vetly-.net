using Vetly.Application.DTOs.Avaliacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Avaliação do atendimento e a reputação que sai dela (RN-055/RN-057).
///
/// Só avalia quem foi atendido, e só uma vez por consulta. Sem esse vínculo, a nota
/// vira número que qualquer um pode empurrar para cima ou para baixo.
/// </summary>
public class AvaliacaoService : IAvaliacaoService
{
    private readonly IAvaliacaoRepository _repo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IUsuarioAtual _usuario;

    public AvaliacaoService(
        IAvaliacaoRepository repo,
        IConsultaRepository consultaRepo,
        IVeterinarioRepository vetRepo,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _vetRepo = vetRepo;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<AvaliacaoDto> AvaliarAsync(Guid consultaId, CriarAvaliacaoDto dto)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        // Quem avalia é quem foi atendido. Escopo pelo token, nunca pelo corpo.
        if (!_usuario.EhAdmin && _usuario.TutorId != consulta.TutorId)
            throw new AcessoNegadoException("RN-105", "Esta consulta nao pertence ao seu escopo de acesso.");

        // RN-055: avalia-se o atendimento que aconteceu. Consulta cancelada ou que nem
        // chegou a acontecer não tem o que avaliar.
        if (consulta.Status != StatusConsulta.Realizada)
            throw new BusinessRuleException("RN-055",
                "Somente consultas realizadas podem ser avaliadas.");

        var referencia = consulta.EncerradaEm ?? consulta.DataHora;

        // O prazo existe porque avaliação muito posterior mede memória, não atendimento
        if (DateTime.UtcNow - referencia > Avaliacao.PrazoParaAvaliar)
            throw new BusinessRuleException("RN-055",
                $"O prazo de {Avaliacao.PrazoParaAvaliar.TotalDays:0} dias para avaliar esta consulta ja passou.");

        if (await _repo.ObterDaConsultaAsync(consultaId) is not null)
            throw new ConflitoDeEstadoException("RN-055", "Esta consulta ja foi avaliada.");

        var avaliacao = new Avaliacao(
            consultaId, consulta.TutorId, consulta.VeterinarioId,
            dto.Nota, dto.Comentario, consulta.EmpresaId);

        await _repo.AdicionarAsync(avaliacao);
        await _repo.SalvarAsync();

        // A reputação é recalculada a partir das avaliações, e não incrementada: média
        // acumulada em campo diverge do que está gravado assim que uma avaliação é
        // moderada, criada fora de ordem ou corrigida (RN-057).
        await RecalcularReputacaoAsync(consulta.VeterinarioId);

        return Mapear(avaliacao);
    }

    /// <inheritdoc/>
    public async Task<ReputacaoDto> ObterReputacaoAsync(Guid veterinarioId)
    {
        var vet = await _vetRepo.ObterPorIdAsync(veterinarioId)
            ?? throw new NotFoundException("Veterinario", veterinarioId);

        var avaliacoes = (await _repo.ObterDoVeterinarioAsync(veterinarioId)).ToList();

        return new ReputacaoDto
        {
            VeterinarioId = veterinarioId,
            NotaMedia = vet.NotaMedia,
            NumAvaliacoes = vet.NumAvaliacoes,

            // RN-057: abaixo de 3 avaliações a nota não é pública nem entra no score.
            // Uma nota 5 vinda de uma única avaliação não diz nada sobre o profissional.
            NotaPublica = vet.TemNotaPublica(),
            MinimoParaNotaPublica = MinimoDeAvaliacoes,

            Distribuicao = Enumerable.Range(1, 5)
                .ToDictionary(nota => nota, nota => avaliacoes.Count(a => a.Nota == nota)),

            Avaliacoes = [.. avaliacoes
                .OrderByDescending(a => a.CriadaEm)
                .Select(Mapear)]
        };
    }

    /// <inheritdoc/>
    public async Task<AvaliacaoDto> ResponderAsync(Guid avaliacaoId, ResponderAvaliacaoDto dto)
    {
        var avaliacao = await _repo.ObterPorIdAsync(avaliacaoId)
            ?? throw new NotFoundException("Avaliacao", avaliacaoId);

        // Responde quem foi avaliado, e mais ninguém
        if (!_usuario.EhAdmin && _usuario.VeterinarioId != avaliacao.VeterinarioId)
            throw new AcessoNegadoException("RN-105", "Esta avaliacao nao pertence ao seu escopo de acesso.");

        if (avaliacao.RespondidaEm is not null)
            throw new ConflitoDeEstadoException("RN-055", "Esta avaliacao ja foi respondida.");

        avaliacao.Responder(dto.Resposta);

        _repo.Atualizar(avaliacao);
        await _repo.SalvarAsync();

        return Mapear(avaliacao);
    }

    /// <inheritdoc/>
    public async Task<AvaliacaoDto> ModerarAsync(Guid avaliacaoId, ModerarAvaliacaoDto dto)
    {
        if (!_usuario.EhAdmin)
            throw new AcessoNegadoException("RN-106", "Somente a administracao modera avaliacoes.");

        var avaliacao = await _repo.ObterPorIdAsync(avaliacaoId)
            ?? throw new NotFoundException("Avaliacao", avaliacaoId);

        // A nota continua contando na média: esconder o texto não pode virar um jeito
        // de apagar uma avaliação ruim.
        avaliacao.ModerarComentario(dto.Motivo);

        _repo.Atualizar(avaliacao);
        await _repo.SalvarAsync();

        return Mapear(avaliacao);
    }

    /// <summary>Mínimo de avaliações para a nota valer publicamente (RN-057).</summary>
    private const int MinimoDeAvaliacoes = 3;

    /// <summary>
    /// Recalcula a reputação a partir das avaliações gravadas (RN-057).
    ///
    /// Recalcular em vez de incrementar: média acumulada em campo diverge do que está
    /// gravado assim que uma avaliação é moderada, criada fora de ordem ou corrigida.
    /// O custo é uma soma sobre as avaliações de um profissional, o que é barato.
    /// </summary>
    private async Task RecalcularReputacaoAsync(Guid veterinarioId)
    {
        var vet = await _vetRepo.ObterPorIdAsync(veterinarioId);

        if (vet is null)
            return;

        var avaliacoes = (await _repo.ObterDoVeterinarioAsync(veterinarioId)).ToList();

        var media = avaliacoes.Count == 0
            ? 0m
            : Math.Round(avaliacoes.Average(a => (decimal)a.Nota), 2);

        vet.AtualizarReputacao(media, avaliacoes.Count);

        _vetRepo.Atualizar(vet);
        await _vetRepo.SalvarAsync();
    }

    private static AvaliacaoDto Mapear(Avaliacao a) => new()
    {
        Id = a.Id,
        ConsultaId = a.ConsultaId,
        VeterinarioId = a.VeterinarioId,
        EmpresaId = a.EmpresaId,
        Nota = a.Nota,
        Comentario = a.ComentarioPublico(),
        ComentarioModerado = a.ComentarioModerado,
        RespostaDoVeterinario = a.RespostaDoVeterinario,
        RespondidaEm = a.RespondidaEm,
        CriadaEm = a.CriadaEm
    };
}
