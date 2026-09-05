using Vetly.Application.DTOs.Colmeia;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Colmeia: o histórico do animal atravessando clínicas, sob autorização do
/// Responsável (RN-090/RN-105).
///
/// Quem concede é o Responsável, quem usa é o veterinário, e todo acesso fica
/// registrado. As três coisas andam juntas: autorização sem registro seria um cheque
/// em branco, e registro sem autorização não seria acesso, seria vazamento.
/// </summary>
public class ColmeiaService : IColmeiaService
{
    private readonly IColmeiaRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IUsuarioAtual _usuario;

    public ColmeiaService(
        IColmeiaRepository repo,
        IAnimalRepository animalRepo,
        IVeterinarioRepository vetRepo,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _vetRepo = vetRepo;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<AcessoColmeiaDto> ConcederAsync(ConcederAcessoDto dto)
    {
        var animal = await _animalRepo.ObterPorIdAsync(dto.AnimalId)
            ?? throw new NotFoundException("Animal", dto.AnimalId);

        // Só o Responsável concede: o histórico é dele, e a clínica que quisesse se
        // autoconceder acesso é exatamente o que esta guarda impede (RN-090)
        if (!_usuario.EhAdmin && (!_usuario.EhTutor || _usuario.TutorId != animal.TutorId))
            throw new AcessoNegadoException("RN-105",
                "Somente o Responsavel pelo animal pode conceder acesso ao historico.");

        var vet = await _vetRepo.ObterPorIdAsync(dto.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", dto.VeterinarioId);

        if (!vet.Ativo)
            throw new BusinessRuleException("RN-090",
                "Nao e possivel conceder acesso a um veterinario desativado.");

        // Concessão viva para o mesmo par não é renovada em silêncio: o Responsável
        // veria duas autorizações e não saberia qual vale.
        var existente = await _repo.ObterVigenteAsync(dto.AnimalId, dto.VeterinarioId, DateTime.UtcNow);

        if (existente is not null)
            throw new ConflitoDeEstadoException("RN-090",
                "Ja existe uma autorizacao vigente para este veterinario neste animal.");

        var validade = dto.ValidadeEmDias is { } dias ? TimeSpan.FromDays(dias) : (TimeSpan?)null;

        var acesso = new AcessoColmeia(
            dto.AnimalId, animal.TutorId, dto.VeterinarioId, dto.Escopo, validade, vet.EmpresaId, dto.Motivo);

        await _repo.AdicionarAsync(acesso);
        await _repo.SalvarAsync();

        return Mapear(acesso);
    }

    /// <inheritdoc/>
    public async Task<AcessoColmeiaDto> RevogarAsync(Guid acessoId)
    {
        var acesso = await _repo.ObterPorIdAsync(acessoId)
            ?? throw new NotFoundException("Acesso da colmeia", acessoId);

        if (!_usuario.EhAdmin && (!_usuario.EhTutor || _usuario.TutorId != acesso.TutorId))
            throw new AcessoNegadoException("RN-105",
                "Somente o Responsavel que concedeu pode revogar o acesso.");

        // Revogar não apaga o que já foi acessado: o log continua, e é isso que o
        // Responsável precisa poder conferir depois (RN-062)
        acesso.Revogar();
        _repo.Atualizar(acesso);
        await _repo.SalvarAsync();

        return Mapear(acesso);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<AcessoColmeiaDto>> ListarDoAnimalAsync(Guid animalId)
    {
        var animal = await _animalRepo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        GarantirQueEOResponsavel(animal);

        var acessos = await _repo.ObterDoAnimalAsync(animalId);

        return acessos.Select(Mapear);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LogAcessoColmeiaDto>> ObterLogDoAnimalAsync(Guid animalId)
    {
        var animal = await _animalRepo.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        GarantirQueEOResponsavel(animal);

        var registros = await _repo.ObterLogDoAnimalAsync(animalId);

        return registros.Select(l => new LogAcessoColmeiaDto
        {
            Id = l.Id,
            AnimalId = l.AnimalId,
            VeterinarioId = l.VeterinarioId,
            Escopo = l.Escopo,
            Rota = l.Rota,
            Permitido = l.Permitido,
            OcorridoEm = l.OcorridoEm
        });
    }

    /// <inheritdoc/>
    public async Task<bool> PodeAcessarAsync(Guid veterinarioId, Guid animalId, EscopoAcessoColmeia escopo)
    {
        var acesso = await _repo.ObterVigenteAsync(animalId, veterinarioId, DateTime.UtcNow);

        return acesso is not null && acesso.Alcanca(escopo);
    }

    /// <inheritdoc/>
    public Task RegistrarAcessoAsync(Guid animalId, EscopoAcessoColmeia escopo, bool permitido, string? rota) =>
        GravarAcessoAsync(_usuario.VeterinarioId, animalId, escopo, permitido, rota);

    /// <inheritdoc/>
    public Task RegistrarAcessoAsync(
        Guid veterinarioId, Guid animalId, EscopoAcessoColmeia escopo, bool permitido, string? rota) =>
        GravarAcessoAsync(veterinarioId, animalId, escopo, permitido, rota);

    /// <summary>
    /// Grava a entrada na trilha. O ator vem de fora porque nem todo acesso nasce numa
    /// requisição: na borda HTTP ele sai do token, e num job precisa ser dito.
    /// </summary>
    private async Task GravarAcessoAsync(
        Guid? veterinarioId, Guid animalId, EscopoAcessoColmeia escopo, bool permitido, string? rota)
    {
        // Tentativa negada também fica registrada: é justamente o que se quer enxergar
        // numa auditoria (RN-090)
        var acesso = veterinarioId is { } vetId
            ? await _repo.ObterVigenteAsync(animalId, vetId, DateTime.UtcNow)
            : null;

        await _repo.AdicionarLogAsync(new LogAcessoColmeia(
            animalId, veterinarioId, escopo, permitido, acesso?.Id, rota));

        await _repo.SalvarAsync();
    }

    /// <summary>
    /// <inheritdoc/>
    public async Task<AcessoColmeiaDto?> EstenderAsync(Guid animalId, Guid veterinarioId, DateTime ate)
    {
        var acesso = await _repo.ObterVigenteAsync(animalId, veterinarioId, DateTime.UtcNow);

        // Sem autorização vigente não há o que estender. Criar uma aqui seria a clínica
        // se autoconcedendo acesso — exatamente o que a guarda de ConcederAsync impede,
        // contornada por outra porta (RN-090).
        if (acesso is null)
            return null;

        acesso.Prorrogar(ate);

        _repo.Atualizar(acesso);
        await _repo.SalvarAsync();

        return Mapear(acesso);
    }

    /// <inheritdoc/>
    public async Task<AcessoColmeiaDto?> AbrirParaAtendimentoAsync(
        Guid animalId, Guid veterinarioId, DateTime ate)
    {
        var animal = await _animalRepo.ObterPorIdAsync(animalId);

        if (animal is null)
            return null;

        var agora = DateTime.UtcNow;

        // Já há autorização viva: prorrogar até o fim do atendimento é o suficiente, e
        // criar uma segunda deixaria o Responsável com duas linhas na tela sem saber
        // qual vale (RN-090).
        var existente = await _repo.ObterVigenteAsync(animalId, veterinarioId, agora);

        if (existente is not null)
        {
            existente.Prorrogar(ate);
            _repo.Atualizar(existente);
            await _repo.SalvarAsync();

            return Mapear(existente);
        }

        var validade = ate > agora ? ate - agora : AcessoColmeia.ValidadePadrao;

        var vet = await _vetRepo.ObterPorIdAsync(veterinarioId);

        // Escopo minimo, e nao historico completo: o profissional que vai atender
        // recebe o ultimo atendimento, o bastante para nao comecar as cegas. Para ver
        // o historico inteiro de outra clinica, o Responsavel concede explicitamente
        // — e essa concessao continua sendo so dele (RN-066).
        var acesso = new AcessoColmeia(
            animalId, animal.TutorId, veterinarioId, EscopoAcessoColmeia.UltimaConsulta,
            validade, vet?.EmpresaId, "Concedido automaticamente pelo agendamento");

        await _repo.AdicionarAsync(acesso);
        await _repo.SalvarAsync();

        return Mapear(acesso);
    }

    /// <summary>
    /// As autorizações e o log são do Responsável: é ele quem precisa ver quem alcança
    /// o histórico do seu animal (RN-090/RN-105).
    /// </summary>
    private void GarantirQueEOResponsavel(Animal animal)
    {
        if (_usuario.EhAdmin)
            return;

        if (_usuario.EhTutor && _usuario.TutorId == animal.TutorId)
            return;

        throw new AcessoNegadoException("RN-105",
            "Somente o Responsavel pelo animal alcanca as autorizacoes do historico.");
    }

    private static AcessoColmeiaDto Mapear(AcessoColmeia a) => new()
    {
        Id = a.Id,
        AnimalId = a.AnimalId,
        TutorId = a.TutorId,
        VeterinarioId = a.VeterinarioId,
        EmpresaId = a.EmpresaId,
        Escopo = a.Escopo,
        ConcedidoEm = a.ConcedidoEm,
        ExpiraEm = a.ExpiraEm,
        RevogadoEm = a.RevogadoEm,
        Motivo = a.Motivo,
        Vigente = a.Vigente(DateTime.UtcNow)
    };
}
