using Vetly.Application.DTOs.Avaliacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de avaliações. Orquestra o gatilho de criação (consulta realizada, janela de
/// 7 dias — RN-076), edição (janela de 48h — RN-082), resposta única do vet (RN-079),
/// moderação (RN-080) e o recálculo de reputação ponderada por recência (RN-078) toda
/// vez que o conjunto de avaliações válidas de um veterinário muda.
/// </summary>
public class AvaliacaoService : IAvaliacaoService
{
    private readonly IAvaliacaoRepository _repo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly TimeProvider _timeProvider;

    public AvaliacaoService(
        IAvaliacaoRepository repo, IConsultaRepository consultaRepo,
        IVeterinarioRepository vetRepo, TimeProvider timeProvider)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _vetRepo = vetRepo;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<AvaliacaoDto> ObterPorIdAsync(Guid id)
    {
        var avaliacao = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Avaliacao", id);
        return MapearParaDto(avaliacao);
    }

    /// <inheritdoc/>
    public async Task<AvaliacaoDto> CriarAsync(Guid consultaId, CriarAvaliacaoDto dto)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (await _repo.ObterPorConsultaAsync(consultaId) is not null)
            throw new BusinessRuleException("AVALIACAO-006", "Esta consulta já foi avaliada.");

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        var avaliacao = Domain.Entities.Avaliacao.Criar(
            consultaId, dto.ResponsavelId, consulta.VeterinarioId, consulta.Status, consulta.DataRealizada,
            dto.NotaGeral, dto.NotaAtendimento, dto.NotaPontualidade, dto.NotaEstrutura, dto.NotaCustoBeneficio,
            dto.Comentario, agora);

        await _repo.AdicionarAsync(avaliacao);
        await _repo.SalvarAsync();
        await RecalcularReputacaoAsync(consulta.VeterinarioId, agora);

        return MapearParaDto(avaliacao);
    }

    /// <inheritdoc/>
    public async Task<AvaliacaoDto> EditarAsync(Guid avaliacaoId, EditarAvaliacaoDto dto)
    {
        var avaliacao = await _repo.ObterPorIdAsync(avaliacaoId)
            ?? throw new NotFoundException("Avaliacao", avaliacaoId);

        var agora = _timeProvider.GetUtcNow().UtcDateTime;
        avaliacao.Editar(
            dto.NotaGeral, dto.NotaAtendimento, dto.NotaPontualidade,
            dto.NotaEstrutura, dto.NotaCustoBeneficio, dto.Comentario, agora);

        _repo.Atualizar(avaliacao);
        await _repo.SalvarAsync();
        await RecalcularReputacaoAsync(avaliacao.VeterinarioId, agora);

        return MapearParaDto(avaliacao);
    }

    /// <inheritdoc/>
    public async Task<AvaliacaoDto> ResponderAsync(Guid avaliacaoId, ResponderAvaliacaoDto dto)
    {
        var avaliacao = await _repo.ObterPorIdAsync(avaliacaoId)
            ?? throw new NotFoundException("Avaliacao", avaliacaoId);

        avaliacao.Responder(dto.Resposta, _timeProvider.GetUtcNow().UtcDateTime);
        _repo.Atualizar(avaliacao);
        await _repo.SalvarAsync();

        return MapearParaDto(avaliacao);
    }

    /// <inheritdoc/>
    public async Task<AvaliacaoDto> ModerarAsync(Guid avaliacaoId, ModerarAvaliacaoDto dto)
    {
        var avaliacao = await _repo.ObterPorIdAsync(avaliacaoId)
            ?? throw new NotFoundException("Avaliacao", avaliacaoId);

        avaliacao.Moderar(dto.StatusModeracao);
        _repo.Atualizar(avaliacao);
        await _repo.SalvarAsync();

        return MapearParaDto(avaliacao);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AvaliacaoDto>> ObterPorVeterinarioAsync(Guid veterinarioId)
    {
        var avaliacoes = await _repo.ObterValidasPorVeterinarioAsync(veterinarioId);
        return avaliacoes.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task InvalidarPorCancelamentoAsync(Guid consultaId, DateTime agora)
    {
        var avaliacao = await _repo.ObterPorConsultaAsync(consultaId);
        if (avaliacao is null || avaliacao.Invalidada)
            return;

        avaliacao.Invalidar();
        _repo.Atualizar(avaliacao);
        await _repo.SalvarAsync();
        await RecalcularReputacaoAsync(avaliacao.VeterinarioId, agora);
    }

    private async Task RecalcularReputacaoAsync(Guid veterinarioId, DateTime agora)
    {
        var vet = await _vetRepo.ObterPorIdAsync(veterinarioId)
            ?? throw new NotFoundException("Veterinario", veterinarioId);

        var validas = await _repo.ObterValidasPorVeterinarioAsync(veterinarioId);
        vet.RecalcularReputacao(validas.Select(a => (a.NotaGeral, a.Data)), agora);
        _vetRepo.Atualizar(vet);
        await _vetRepo.SalvarAsync();
    }

    private static AvaliacaoDto MapearParaDto(Domain.Entities.Avaliacao a) => new()
    {
        Id = a.Id, ConsultaId = a.ConsultaId, ResponsavelId = a.ResponsavelId, VeterinarioId = a.VeterinarioId,
        NotaGeral = a.NotaGeral, NotaAtendimento = a.NotaAtendimento, NotaPontualidade = a.NotaPontualidade,
        NotaEstrutura = a.NotaEstrutura, NotaCustoBeneficio = a.NotaCustoBeneficio, Comentario = a.Comentario,
        Data = a.Data, StatusModeracao = a.StatusModeracao, RespostaVeterinario = a.RespostaVeterinario,
        DataResposta = a.DataResposta, Invalidada = a.Invalidada
    };
}
