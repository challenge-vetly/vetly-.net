using Vetly.Application.DTOs.Documento;
using Vetly.Application.DTOs.Obrigacao;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Exame;
using Vetly.Application.DTOs.Prontuario;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>Servico de animais. Gerencia cadastro, historico longitudinal e exames.</summary>
public class AnimalService : IAnimalService
{
    private readonly IAnimalRepository _repo;
    private readonly IColmeiaService _colmeia;
    private readonly IObrigacaoService _obrigacoes;
    private readonly IDocumentoRepository _documentos;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IUsuarioAtual _usuario;

    public AnimalService(
        IAnimalRepository repo,
        IColmeiaService colmeia,
        IObrigacaoService obrigacoes,
        IDocumentoRepository documentos,
        IVeterinarioRepository vetRepo,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _colmeia = colmeia;
        _obrigacoes = obrigacoes;
        _documentos = documentos;
        _vetRepo = vetRepo;
        _usuario = usuario;
    }

    /// <summary>
    /// Lista os animais dentro do escopo de quem chama (RN-105/RN-106):
    /// o Responsável vê os seus, o veterinário vê os que atendeu ou tem agendados,
    /// e só o Admin vê todos.
    /// </summary>
    public async Task<IEnumerable<AnimalDto>> ObterTodosAsync()
    {
        var animais = await ObterNoEscopoAsync();
        return animais.Select(MapearParaDto);
    }

    public async Task<AnimalDto> ObterPorIdAsync(Guid id)
    {
        var animal = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Animal", id);

        await GarantirAcessoAoAnimalAsync(animal);
        return MapearParaDto(animal);
    }

    /// <summary>Aplica o escopo do usuário atual sobre a listagem.</summary>
    private async Task<IEnumerable<Animal>> ObterNoEscopoAsync()
    {
        if (_usuario.EhAdmin)
            return await _repo.ObterAtivosAsync();

        if (_usuario.EhTutor && _usuario.TutorId is { } tutorId)
            return await _repo.ObterPorTutorAsync(tutorId);

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId)
            return await _repo.ObterPorVeterinarioAsync(vetId);

        // Token autenticado sem escopo reconhecido nao ve nada. Falhar fechado e o
        // comportamento certo aqui: dado de saude e sensivel (RN-069).
        return [];
    }

    /// <summary>
    /// Recusa o acesso a animal fora do escopo (RN-105). O Responsável só alcança os
    /// seus; o veterinário, os que atendeu ou tem agendados.
    /// </summary>
    private async Task GarantirAcessoAoAnimalAsync(Animal animal)
    {
        if (_usuario.EhAdmin)
            return;

        if (_usuario.EhTutor && _usuario.TutorId == animal.TutorId)
            return;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId)
        {
            if (await _repo.VeterinarioAtendeAnimalAsync(vetId, animal.Id))
                return;

            // Colmeia: o veterinario de fora alcanca o historico se o Responsavel
            // autorizou, e o acesso — permitido ou negado — fica registrado (RN-090).
            var autorizado = await _colmeia.PodeAcessarAsync(
                vetId, animal.Id, EscopoAcessoColmeia.HistoricoCompleto);

            await _colmeia.RegistrarAcessoAsync(
                animal.Id, EscopoAcessoColmeia.HistoricoCompleto, autorizado, "AnimalService");

            if (autorizado)
                return;
        }

        throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");
    }

    /// <inheritdoc/>
    public async Task<BoardDoPetDto> ObterBoardAsync(Guid animalId)
    {
        var animal = await _repo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        await GarantirAcessoAoAnimalAsync(animal);

        var agora = DateTime.UtcNow;

        var board = await _obrigacoes.ObterBoardAsync(animalId);
        var consultas = await _repo.ObterConsultasFuturasAsync(animalId, agora);
        var documentos = await _documentos.ObterPublicadosPorAnimalAsync(animalId);

        var proximos = new List<AgendamentoDoBoardDto>();

        foreach (var consulta in consultas.OrderBy(c => c.DataHora).Take(5))
        {
            var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId);

            proximos.Add(new AgendamentoDoBoardDto
            {
                ConsultaId = consulta.Id,
                DataHora = consulta.DataHora,
                VeterinarioId = consulta.VeterinarioId,
                VeterinarioNome = vet?.Nome ?? "Profissional nao encontrado",
                Status = consulta.Status,
                Modalidade = consulta.Modalidade
            });
        }

        return new BoardDoPetDto
        {
            AnimalId = animal.Id,
            Nome = animal.Nome,
            Especie = animal.Especie,
            Raca = animal.Raca,
            IdadeAnos = Math.Max(0, (int)((agora - animal.DataNascimento).TotalDays / 365.25)),
            PesoKg = animal.PesoKg,
            FotoMidiaId = animal.FotoMidiaId,
            AvatarEstado = DeduzirAvatar(board),
            Obrigacoes = board.Obrigacoes,
            TemPendencia = board.TemPendencia,
            ProximosAgendamentos = proximos,

            // Recentes, e nao todos: o board e tela de entrada, nao arquivo
            DocumentosRecentes = [.. documentos.Take(5).Select(d => new DocumentoDto
            {
                Id = d.Id,
                ConsultaId = d.ConsultaId,
                TipoDocumento = d.TipoDocumento,
                Versao = d.Versao,
                DataGeracao = d.DataGeracao,
                CrmvSignatario = d.CrmvSignatario,
                AssinadoDigitalmente = d.AssinadoDigitalmente,
                Subtipo = d.Subtipo,
                PdfMidiaId = d.PdfMidiaId,
                PublicadoEm = d.PublicadoEm,
                LidoEm = d.LidoEm
            })],

            // RN-068: alerta de seguranca nunca e ocultavel, e por isso vem sempre
            AlertasDeSeguranca = [.. animal.AlertasAtivos, .. animal.Alergias]
        };
    }

    /// <summary>
    /// Deriva o estado do avatar das obrigacoes vencidas (RN-020/RN-096/RN-097).
    ///
    /// Vacina tem precedencia sobre higiene: antirrabica atrasada e questao sanitaria,
    /// banho atrasado e desconforto. Quando as duas estao vencidas, o avatar mostra a
    /// que importa mais.
    ///
    /// E o unico dado do avatar que a API produz — o sprite e a animacao sao assets do
    /// app (C3).
    /// </summary>
    private static EstadoDoAvatar DeduzirAvatar(BoardDeObrigacoesDto board)
    {
        var vencidas = board.Obrigacoes
            .Where(o => o.Situacao == SituacaoObrigacao.Vencida)
            .ToList();

        if (vencidas.Any(o => o.Tipo == TipoObrigacaoPet.Vacina))
            return EstadoDoAvatar.VacinaAtrasada;

        if (vencidas.Count > 0)
            return EstadoDoAvatar.HigieneAtrasada;

        return EstadoDoAvatar.Saudavel;
    }

    public async Task<IEnumerable<ProntuarioDto>> ObterHistoricoAsync(Guid animalId)
    {
        var animal = await _repo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);
        await GarantirAcessoAoAnimalAsync(animal);

        var prontuarios = await _repo.ObterHistoricoLongitudinalAsync(animalId);
        return prontuarios.Select(p => new ProntuarioDto
        {
            Id = p.Id, ConsultaId = p.ConsultaId, AnimalId = p.AnimalId,
            DadosClinicos = p.DadosClinicos, VersaoOriginalId = p.VersaoOriginalId,
            DataCorrecao = p.DataCorrecao, JustificativaCorrecao = p.JustificativaCorrecao,
            CrmvSolicitanteCorrecao = p.CrmvSolicitanteCorrecao,
            DataCriacao = p.DataCriacao, ExigeJustificativa = p.ExigeJustificativa()
        });
    }

    public async Task<IEnumerable<ExameDto>> ObterExamesAsync(Guid animalId)
    {
        var animal = await _repo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);
        await GarantirAcessoAoAnimalAsync(animal);

        var exames = await _repo.ObterExamesAsync(animalId);
        return exames.Select(e => new ExameDto
        {
            Id = e.Id, AnimalId = e.AnimalId, VeterinarioId = e.VeterinarioId,
            TipoSolicitacao = e.TipoSolicitacao, Resultado = e.Resultado,
            LiberadoAoTutor = e.LiberadoAoTutor,
            DataSolicitacao = e.DataSolicitacao, DataResultado = e.DataResultado
        });
    }

    public async Task<AnimalDto> CriarAsync(CriarAnimalDto dto)
    {
        // Responsavel so cadastra pet no proprio nome; passar o tutorId de outro no
        // corpo da requisicao nao pode funcionar (RN-105).
        if (_usuario.EhTutor && _usuario.TutorId != dto.TutorId)
            throw new AcessoNegadoException("RN-105", "Nao e possivel cadastrar animal para outro responsavel.");

        var animal = new Animal(dto.Nome, dto.Especie, dto.Raca, dto.DataNascimento, dto.TutorId);
        AplicarPerfilClinico(animal, dto);

        await _repo.AdicionarAsync(animal);
        await _repo.SalvarAsync();
        return MapearParaDto(animal);
    }

    public async Task AtualizarAsync(Guid id, CriarAnimalDto dto)
    {
        var animal = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Animal", id);
        await GarantirAcessoAoAnimalAsync(animal);

        animal.AtualizarDados(dto.Nome, dto.Especie, dto.Raca, dto.DataNascimento);
        AplicarPerfilClinico(animal, dto);

        _repo.Atualizar(animal);
        await _repo.SalvarAsync();
    }

    public async Task DesativarAsync(Guid id)
    {
        var animal = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Animal", id);
        await GarantirAcessoAoAnimalAsync(animal);

        animal.Desativar();
        _repo.Atualizar(animal);
        await _repo.SalvarAsync();
    }

    /// <summary>
    /// Transfere o perfil clínico do DTO para a entidade. O peso passa por
    /// <c>RegistrarPeso</c>, que rejeita valor não positivo (RN-081).
    /// </summary>
    private static void AplicarPerfilClinico(Animal animal, CriarAnimalDto dto)
    {
        // O [Range] do DTO ja barra peso invalido no controller; esta guarda cobre as chamadas
        // que nao passam pela validacao do model binder e devolve 422 com o codigo da RN,
        // em vez do 500 que a ArgumentOutOfRangeException do dominio produziria.
        if (dto.PesoKg <= 0)
            throw new BusinessRuleException("RN-081",
                "O peso do animal e obrigatorio e deve ser maior que zero — sem ele a IA nao pode sugerir dose.");

        animal.RegistrarPeso(dto.PesoKg);
        animal.DefinirPerfilClinico(
            dto.Sexo, dto.Castrado, dto.FotoMidiaId, dto.Alergias, dto.CondicoesPreexistentes);
        animal.DefinirCarteiraVacinacao(
            dto.CarteiraVacinacao.Select(v => new RegistroVacinacao(v.Tipo, v.AplicadaEm)));
    }

    private static AnimalDto MapearParaDto(Animal a) => new()
    {
        Id = a.Id, Nome = a.Nome, Especie = a.Especie, Raca = a.Raca,
        DataNascimento = a.DataNascimento, IdadeEmAnos = a.IdadeEmAnos(),
        TutorId = a.TutorId, AlertasAtivos = a.AlertasAtivos,
        PesoKg = a.PesoKg, Sexo = a.Sexo, Castrado = a.Castrado, FotoMidiaId = a.FotoMidiaId,
        Alergias = a.Alergias, CondicoesPreexistentes = a.CondicoesPreexistentes,
        CarteiraVacinacao = [.. a.CarteiraVacinacao.Select(v => new RegistroVacinacaoDto
        {
            Tipo = v.Tipo, AplicadaEm = v.AplicadaEm
        })],
        Ativo = a.Ativo
    };
}
