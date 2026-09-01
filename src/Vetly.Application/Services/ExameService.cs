using Vetly.Application.DTOs.Exame;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de exames: solicitação, resultado e liberação ao Responsável
/// (RN-103/RN-104).
///
/// O ponto central do escopo aqui é a RN-104: o resultado só chega ao Responsável
/// <b>após liberação explícita do veterinário</b>. Um exame com resultado gravado e
/// não liberado é dado clínico que ainda não foi interpretado — mostrá-lo ao
/// Responsável antes disso transforma um número solto em diagnóstico caseiro.
/// </summary>
public class ExameService : IExameService
{
    private readonly IExameRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly INotificacaoService _notificacoes;
    private readonly IUsuarioAtual _usuario;

    public ExameService(
        IExameRepository repo,
        IAnimalRepository animalRepo,
        INotificacaoService notificacoes,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _notificacoes = notificacoes;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ExameDto>> ObterTodosAsync()
    {
        if (_usuario.EhAdmin)
            return (await _repo.ObterTodosAsync()).Select(MapearParaDto);

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId)
            return (await _repo.ObterPorVeterinarioAsync(vetId)).Select(MapearParaDto);

        if (_usuario.EhTutor && _usuario.TutorId is { } tutorId)
        {
            var animais = await _animalRepo.ObterPorTutorAsync(tutorId);
            var exames = new List<Exame>();

            foreach (var animal in animais)
                exames.AddRange(await _repo.ObterPorAnimalAsync(animal.Id));

            // RN-104: o Responsável só enxerga o que o veterinário liberou
            return exames.Where(e => e.LiberadoAoTutor).Select(MapearParaDto);
        }

        // Token autenticado sem escopo reconhecido não vê nada. Falhar fechado é o
        // comportamento certo aqui: dado de saúde é sensível (RN-069).
        return [];
    }

    /// <inheritdoc/>
    public async Task<ExameDto> ObterPorIdAsync(Guid id)
    {
        var exame = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Exame", id);

        await GarantirLeituraAsync(exame);

        return MapearParaDto(exame);
    }

    /// <inheritdoc/>
    public async Task<ExameDto> CriarAsync(CriarExameDto dto)
    {
        var veterinarioId = ResolverSolicitante(dto.VeterinarioId);

        var animal = await _animalRepo.ObterPorIdAsync(dto.AnimalId)
            ?? throw new NotFoundException("Animal", dto.AnimalId);

        var exame = new Exame(dto.AnimalId, veterinarioId, dto.TipoSolicitacao);

        await _repo.AdicionarAsync(exame);
        await _repo.SalvarAsync();

        // RN-103: o Responsável precisa das orientações de preparo ANTES do exame —
        // jejum e coleta têm janela, e avisar depois não serve para nada.
        await _notificacoes.CriarAsync(new CriarNotificacaoDto
        {
            TutorId = animal.TutorId,
            Tipo = TipoNotificacao.ExameSolicitado,
            Titulo = "Exame solicitado",
            Corpo = $"{dto.TipoSolicitacao} foi solicitado para {animal.Nome}. " +
                    "Toque para ver as orientacoes de preparo.",
            AnimalId = animal.Id,
            Destino = $"/animais/{animal.Id}/exames"
        });

        return MapearParaDto(exame);
    }

    /// <inheritdoc/>
    public async Task<ExameDto> RegistrarResultadoAsync(Guid id, string resultado, IEnumerable<Guid>? midiaIds = null)
    {
        var exame = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Exame", id);

        GarantirEscritaDoSolicitante(exame);

        exame.RegistrarResultado(resultado); // lanca InvalidOperationException se vazio
        exame.AnexarMidias(midiaIds);

        _repo.Atualizar(exame);
        await _repo.SalvarAsync();

        // Sem notificação aqui de propósito: o resultado existe, mas não foi liberado.
        // Avisar agora faria o Responsável abrir o app e não encontrar nada (RN-104).
        return MapearParaDto(exame);
    }

    /// <inheritdoc/>
    public async Task LiberarAoTutorAsync(Guid id)
    {
        var exame = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Exame", id);

        GarantirEscritaDoSolicitante(exame);

        exame.LiberarAoTutor(); // lanca InvalidOperationException se sem resultado

        _repo.Atualizar(exame);
        await _repo.SalvarAsync();

        var animal = await _animalRepo.ObterPorIdAsync(exame.AnimalId);

        // RN-104/RN-090: só agora o Responsável é avisado — o resultado passou pela
        // leitura do profissional e chega interpretado.
        if (animal is not null)
        {
            await _notificacoes.CriarAsync(new CriarNotificacaoDto
            {
                TutorId = animal.TutorId,
                Tipo = TipoNotificacao.DocumentoPublicado,
                Titulo = "Resultado de exame disponivel",
                Corpo = $"O resultado de {exame.TipoSolicitacao} de {animal.Nome} ja esta no app.",
                AnimalId = animal.Id,
                Destino = $"/animais/{animal.Id}/exames"
            });
        }
    }

    /// <summary>
    /// Quem lê um exame (RN-104/RN-105/RN-106): o Admin, o veterinário que o
    /// solicitou, e o Responsável — este último apenas depois da liberação.
    /// </summary>
    private async Task GarantirLeituraAsync(Exame exame)
    {
        if (_usuario.EhAdmin)
            return;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId == exame.VeterinarioId)
            return;

        if (_usuario.EhTutor && _usuario.TutorId is { } tutorId)
        {
            var animal = await _animalRepo.ObterPorIdAsync(exame.AnimalId);

            if (animal?.TutorId == tutorId)
            {
                if (exame.LiberadoAoTutor)
                    return;

                // 403 e não 404: o Responsável sabe que o exame existe — ele o pediu.
                // O que ainda não existe para ele é o resultado.
                throw new AcessoNegadoException("RN-104",
                    "O resultado ainda nao foi liberado pelo veterinario.");
            }
        }

        throw new AcessoNegadoException("RN-105", "Este exame nao pertence ao seu escopo de acesso.");
    }

    /// <summary>
    /// Quem escreve num exame é quem o solicitou, ou o Admin (RN-105). O Responsável
    /// nunca: registrar resultado ou liberar a si mesmo esvaziaria a RN-104.
    /// </summary>
    private void GarantirEscritaDoSolicitante(Exame exame)
    {
        if (_usuario.EhAdmin || (_usuario.EhVeterinario && _usuario.VeterinarioId == exame.VeterinarioId))
            return;

        throw new AcessoNegadoException("RN-105", "Este exame nao pertence ao seu escopo de acesso.");
    }

    /// <summary>
    /// O solicitante vem do token quando quem chama é veterinário: pedir exame em nome
    /// de outro profissional deixaria o histórico do animal apontando para quem não
    /// participou do atendimento (RN-105).
    /// </summary>
    private Guid ResolverSolicitante(Guid veterinarioIdDoPedido)
    {
        if (_usuario.EhAdmin)
            return veterinarioIdDoPedido;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId)
            return vetId;

        throw new AcessoNegadoException("RN-105", "Somente o veterinario ou a administracao solicita exames.");
    }

    private static ExameDto MapearParaDto(Exame e) => new()
    {
        Id = e.Id,
        AnimalId = e.AnimalId,
        VeterinarioId = e.VeterinarioId,
        TipoSolicitacao = e.TipoSolicitacao,
        Resultado = e.Resultado,
        MidiaIds = [.. e.Midias()],
        LiberadoAoTutor = e.LiberadoAoTutor,
        DataSolicitacao = e.DataSolicitacao,
        DataResultado = e.DataResultado
    };
}
