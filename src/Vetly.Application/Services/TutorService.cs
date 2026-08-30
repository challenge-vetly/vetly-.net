using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Tutor;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>Servico de tutores. Gerencia cadastro e consentimentos LGPD.</summary>
public class TutorService : ITutorService
{
    private readonly ITutorRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IUsuarioAtual _usuario;

    public TutorService(ITutorRepository repo, IAnimalRepository animalRepo, IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _usuario = usuario;
    }

    /// <summary>
    /// Recusa acesso aos dados de outro Responsável (RN-105/RN-106). O Admin passa;
    /// o Tutor só alcança o próprio cadastro.
    /// </summary>
    private void GarantirPosse(Guid tutorId)
    {
        if (_usuario.EhAdmin || _usuario.TutorId == tutorId)
            return;

        throw new AcessoNegadoException("RN-106", "Este cadastro nao pertence ao seu escopo de acesso.");
    }

    public async Task<IEnumerable<TutorDto>> ObterTodosAsync()
    {
        var tutores = await _repo.ObterAtivosAsync();
        return tutores.Select(MapearParaDto);
    }

    public async Task<TutorDto> ObterPorIdAsync(Guid id)
    {
        GarantirPosse(id);

        var tutor = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Tutor", id);
        return MapearParaDto(tutor);
    }

    public async Task<IEnumerable<AnimalDto>> ObterAnimaisAsync(Guid tutorId)
    {
        GarantirPosse(tutorId);

        _ = await _repo.ObterPorIdAsync(tutorId)
            ?? throw new NotFoundException("Tutor", tutorId);

        var animais = await _animalRepo.ObterPorTutorAsync(tutorId);
        return animais.Select(a => new AnimalDto
        {
            Id = a.Id, Nome = a.Nome, Especie = a.Especie, Raca = a.Raca,
            DataNascimento = a.DataNascimento, IdadeEmAnos = a.IdadeEmAnos(),
            TutorId = a.TutorId, AlertasAtivos = a.AlertasAtivos, Ativo = a.Ativo
        });
    }

    public async Task<TutorDto> CriarAsync(CriarTutorDto dto)
    {
        var existente = await _repo.ObterPorEmailAsync(dto.Email);
        if (existente is not null)
            throw new BusinessRuleException("TUTOR-001", $"E-mail '{dto.Email}' ja esta cadastrado.");

        var tutor = new Tutor(dto.Nome, dto.Email, dto.Telefone);
        await _repo.AdicionarAsync(tutor);
        await _repo.SalvarAsync();
        return MapearParaDto(tutor);
    }

    public async Task AtualizarAsync(Guid id, CriarTutorDto dto)
    {
        GarantirPosse(id);

        var tutor = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Tutor", id);
        tutor.AtualizarDados(dto.Nome, dto.Email, dto.Telefone);
        _repo.Atualizar(tutor);
        await _repo.SalvarAsync();
    }

    public async Task DesativarAsync(Guid id)
    {
        GarantirPosse(id);

        var tutor = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Tutor", id);
        tutor.Desativar();
        _repo.Atualizar(tutor);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConsentimentoDto>> ObterConsentimentosAsync(Guid tutorId)
    {
        GarantirPosse(tutorId);

        var tutor = await _repo.ObterPorIdAsync(tutorId)
            ?? throw new NotFoundException("Tutor", tutorId);

        return tutor.Consentimentos().Select(MapearConsentimento);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConsentimentoDto>> AtualizarConsentimentosAsync(
        Guid tutorId, AtualizarConsentimentosDto dto)
    {
        GarantirPosse(tutorId);

        var tutor = await _repo.ObterPorIdAsync(tutorId)
            ?? throw new NotFoundException("Tutor", tutorId);

        // Um unico instante para o lote inteiro: as datas de registro precisam ser
        // coerentes entre si na trilha de auditoria da LGPD (RN-062).
        var agora = DateTime.UtcNow;

        foreach (var alteracao in dto.Consentimentos)
            tutor.RegistrarConsentimento(alteracao.Finalidade, alteracao.Concedido, agora);

        _repo.Atualizar(tutor);
        await _repo.SalvarAsync();

        return tutor.Consentimentos().Select(MapearConsentimento);
    }

    private static ConsentimentoDto MapearConsentimento(ConsentimentoRegistrado registro) => new()
    {
        Finalidade = registro.Finalidade,
        Concedido = registro.Concedido,
        ConcedidoEm = registro.ConcedidoEm,
        RevogadoEm = registro.RevogadoEm,
        Descricao = DescreverFinalidade(registro.Finalidade)
    };

    /// <summary>
    /// Texto apresentado na tela de consentimento. A LGPD exige que a finalidade seja
    /// informada de forma clara e acessivel, nao so nomeada (RN-060/RN-061).
    /// </summary>
    private static string DescreverFinalidade(FinalidadeConsentimento finalidade) => finalidade switch
    {
        FinalidadeConsentimento.Atendimento =>
            "Uso dos dados do seu pet para realizar os atendimentos clinicos na plataforma.",
        FinalidadeConsentimento.Lembretes =>
            "Envio de lembretes de vacina, retorno e medicacao, e demais comunicacoes de cuidado.",
        FinalidadeConsentimento.Compartilhamento =>
            "Compartilhamento do historico do seu pet com as clinicas parceiras da rede, para que qualquer veterinario atenda com o contexto completo.",
        FinalidadeConsentimento.Promocoes =>
            "Envio de promocoes e ofertas. Opcional, e voce pode desativar quando quiser.",
        FinalidadeConsentimento.DadosAgregados =>
            "Uso dos dados de forma agregada e anonima em estatisticas. Nao identifica voce nem o seu pet, e desativar nao tira nenhuma funcionalidade do app.",
        _ => string.Empty
    };

    private static TutorDto MapearParaDto(Tutor t) => new()
    {
        Id = t.Id, Nome = t.Nome, Email = t.Email, Telefone = t.Telefone,
        ConsentimentoAtendimento = t.ConsentimentoAtendimento,
        ConsentimentoLembretes = t.ConsentimentoLembretes,
        ConsentimentoCompartilhamento = t.ConsentimentoCompartilhamento,
        DataConsentimento = t.DataConsentimento, Ativo = t.Ativo
    };
}
