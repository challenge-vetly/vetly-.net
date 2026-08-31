using Vetly.Application.DTOs.Obrigacao;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Obrigações de cuidado do animal e o board que as mostra (RN-045/RN-046).
///
/// O Responsável não tem como lembrar sozinho de seis reforços com periodicidades
/// diferentes, e o veterinário só descobre o atraso quando o animal já voltou doente.
/// O board existe para que a pergunta "está tudo em dia?" tenha resposta.
/// </summary>
public class ObrigacaoService : IObrigacaoService
{
    private readonly IObrigacaoRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IFidelidadeService _fidelidade;
    private readonly IUsuarioAtual _usuario;

    /// <summary>
    /// Periodicidade assumida para vacina derivada da carteira, quando ninguém
    /// informou outra. Um ano é o reforço anual da maioria dos protocolos; o
    /// veterinário ajusta no atendimento.
    /// </summary>
    private const int PeriodicidadeAnual = 365;

    public ObrigacaoService(
        IObrigacaoRepository repo,
        IAnimalRepository animalRepo,
        IFidelidadeService fidelidade,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _fidelidade = fidelidade;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<BoardDeObrigacoesDto> ObterBoardAsync(Guid animalId, bool incluirArquivadas = false)
    {
        var animal = await ObterAnimalNoEscopoAsync(animalId);

        var obrigacoes = (await _repo.ObterDoAnimalAsync(animalId, incluirArquivadas)).ToList();

        var agora = DateTime.UtcNow;

        var itens = obrigacoes
            .Select(o => Mapear(o, agora))
            // Vencida primeiro, depois vencendo; dentro de cada grupo, a mais antiga
            // na frente. O board serve para decidir o que fazer, não para catalogar.
            .OrderBy(o => o.Situacao == SituacaoObrigacao.Arquivada ? 1 : 0)
            .ThenBy(o => o.ProximoVencimento)
            .ToList();

        return new BoardDeObrigacoesDto
        {
            AnimalId = animalId,
            AnimalNome = animal.Nome,
            TotalVencidas = itens.Count(i => i.Situacao == SituacaoObrigacao.Vencida),
            TotalVencendo = itens.Count(i => i.Situacao == SituacaoObrigacao.Vencendo),
            TotalEmDia = itens.Count(i => i.Situacao == SituacaoObrigacao.EmDia),
            TemPendencia = itens.Any(i => i.Situacao == SituacaoObrigacao.Vencida),
            Obrigacoes = itens
        };
    }

    /// <inheritdoc/>
    public async Task<ObrigacaoPetDto> CriarAsync(Guid animalId, CriarObrigacaoDto dto)
    {
        var animal = await ObterAnimalNoEscopoAsync(animalId);

        var obrigacao = new ObrigacaoPet(
            animalId, animal.TutorId, dto.Tipo, dto.Descricao,
            dto.ProximoVencimento, dto.PeriodicidadeEmDias);

        await _repo.AdicionarAsync(obrigacao);
        await _repo.SalvarAsync();

        return Mapear(obrigacao, DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<ObrigacaoPetDto> CumprirAsync(Guid obrigacaoId, CumprirObrigacaoDto dto)
    {
        var obrigacao = await _repo.ObterPorIdAsync(obrigacaoId)
            ?? throw new NotFoundException("Obrigacao", obrigacaoId);

        await ObterAnimalNoEscopoAsync(obrigacao.AnimalId);

        var quando = dto.Quando ?? DateTime.UtcNow;

        // Cumprimento no futuro seria registro de algo que não aconteceu, e empurraria
        // o próximo vencimento para longe demais
        if (quando > DateTime.UtcNow.AddDays(1))
            throw new ValidationException("quando", "Nao e possivel registrar cumprimento no futuro.");

        // RN-047: o credito e por cumprir NO PRAZO. Cumprir atrasado resolve a
        // pendencia do animal, mas nao rende os 50 pontos — o bonus paga o
        // comportamento preventivo, nao o corretivo.
        var noPrazo = quando <= obrigacao.ProximoVencimento;

        obrigacao.Cumprir(quando, dto.ConsultaId, _usuario.VeterinarioId);

        _repo.Atualizar(obrigacao);
        await _repo.SalvarAsync();

        if (noPrazo)
        {
            await _fidelidade.CreditarPorObrigacaoAsync(
                obrigacao.TutorId, obrigacao.Id, obrigacao.Descricao);
        }

        return Mapear(obrigacao, DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<ObrigacaoPetDto> ArquivarAsync(Guid obrigacaoId)
    {
        var obrigacao = await _repo.ObterPorIdAsync(obrigacaoId)
            ?? throw new NotFoundException("Obrigacao", obrigacaoId);

        await ObterAnimalNoEscopoAsync(obrigacao.AnimalId);

        obrigacao.Arquivar();
        _repo.Atualizar(obrigacao);
        await _repo.SalvarAsync();

        return Mapear(obrigacao, DateTime.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ObrigacaoPetDto>> DerivarDaCarteiraAsync(Guid animalId)
    {
        var animal = await ObterAnimalNoEscopoAsync(animalId);

        var existentes = (await _repo.ObterDoAnimalAsync(animalId, incluirArquivadas: true)).ToList();

        var criadas = new List<ObrigacaoPet>();

        // Cada tipo de vacina vira uma obrigação, contada a partir da dose mais
        // recente daquele tipo. Doses antigas do mesmo tipo são o histórico, não
        // obrigações separadas.
        foreach (var grupo in animal.CarteiraVacinacao.GroupBy(v => v.Tipo, StringComparer.OrdinalIgnoreCase))
        {
            var ultimaDose = grupo.Max(v => v.AplicadaEm);

            // Já derivada antes: derivar de novo criaria duplicata a cada chamada
            if (existentes.Any(o => o.Tipo == TipoObrigacaoPet.Vacina
                                    && string.Equals(o.Descricao, grupo.Key, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var obrigacao = new ObrigacaoPet(
                animalId, animal.TutorId, TipoObrigacaoPet.Vacina, grupo.Key,
                ultimaDose.AddDays(PeriodicidadeAnual), PeriodicidadeAnual, derivadaDaCarteira: true);

            obrigacao.Cumprir(ultimaDose);

            criadas.Add(obrigacao);
            await _repo.AdicionarAsync(obrigacao);
        }

        if (criadas.Count > 0)
            await _repo.SalvarAsync();

        var agora = DateTime.UtcNow;

        return criadas.Select(o => Mapear(o, agora));
    }

    /// <summary>
    /// O board é do animal do Responsável; o veterinário alcança os que atende
    /// (RN-105/RN-106). O escopo vem do token, nunca do parâmetro.
    /// </summary>
    private async Task<Animal> ObterAnimalNoEscopoAsync(Guid animalId)
    {
        var animal = await _animalRepo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        if (_usuario.EhAdmin)
            return animal;

        if (_usuario.EhTutor && _usuario.TutorId == animal.TutorId)
            return animal;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId
            && await _animalRepo.VeterinarioAtendeAnimalAsync(vetId, animalId))
        {
            return animal;
        }

        throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");
    }

    private static ObrigacaoPetDto Mapear(ObrigacaoPet o, DateTime agora) => new()
    {
        Id = o.Id,
        AnimalId = o.AnimalId,
        Tipo = o.Tipo,
        Descricao = o.Descricao,
        ProximoVencimento = o.ProximoVencimento,
        PeriodicidadeEmDias = o.PeriodicidadeEmDias,
        Situacao = o.SituacaoEm(agora),
        DiasAteVencer = o.DiasAteVencer(agora),
        UltimoCumprimento = o.UltimoCumprimento,
        UltimaConsultaId = o.UltimaConsultaId,
        DerivadaDaCarteira = o.DerivadaDaCarteira,
        Arquivada = o.Arquivada
    };
}
