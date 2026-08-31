using Vetly.Application.DTOs.Colmeia;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Colmeia: o histórico do animal atravessando clínicas, sob autorização do
/// Responsável (RN-090/RN-105).
/// </summary>
public interface IColmeiaService
{
    /// <summary>Concede acesso ao histórico. Só o Responsável concede.</summary>
    Task<AcessoColmeiaDto> ConcederAsync(ConcederAcessoDto dto);

    /// <summary>Revoga a autorização. O log do que já foi acessado permanece.</summary>
    Task<AcessoColmeiaDto> RevogarAsync(Guid acessoId);

    /// <summary>Autorizações de um animal, para o Responsável ver quem alcança o quê.</summary>
    Task<IEnumerable<AcessoColmeiaDto>> ListarDoAnimalAsync(Guid animalId);

    /// <summary>Acessos efetivamente feitos ao histórico do animal (RN-090).</summary>
    Task<IEnumerable<LogAcessoColmeiaDto>> ObterLogDoAnimalAsync(Guid animalId);

    /// <summary>Se há autorização vigente que alcance o escopo pedido.</summary>
    Task<bool> PodeAcessarAsync(Guid veterinarioId, Guid animalId, EscopoAcessoColmeia escopo);

    /// <summary>Registra o acesso na trilha, permitido ou negado.</summary>
    Task RegistrarAcessoAsync(Guid animalId, EscopoAcessoColmeia escopo, bool permitido, string? rota);
}

/// <summary>
/// Repositório da colmeia (RN-090).
///
/// O log é append-only por contrato: há adicionar e ler, e nada mais. Autorização sem
/// registro confiável seria um cheque em branco.
/// </summary>
public interface IColmeiaRepository
{
    Task<AcessoColmeia?> ObterPorIdAsync(Guid id);

    /// <summary>Autorização viva para o par animal/veterinário, se houver.</summary>
    Task<AcessoColmeia?> ObterVigenteAsync(Guid animalId, Guid veterinarioId, DateTime agora);

    /// <summary>Autorizações de um animal, das mais recentes às mais antigas.</summary>
    Task<IEnumerable<AcessoColmeia>> ObterDoAnimalAsync(Guid animalId);

    Task AdicionarAsync(AcessoColmeia acesso);
    void Atualizar(AcessoColmeia acesso);

    /// <summary>Acessos registrados de um animal, do mais recente ao mais antigo.</summary>
    Task<IEnumerable<LogAcessoColmeia>> ObterLogDoAnimalAsync(Guid animalId);

    Task AdicionarLogAsync(LogAcessoColmeia registro);

    Task<int> SalvarAsync();
}
