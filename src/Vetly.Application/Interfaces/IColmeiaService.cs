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

    /// <summary>
    /// Registra o acesso na trilha, permitido ou negado, em nome do veterinário da
    /// requisição atual.
    /// </summary>
    Task RegistrarAcessoAsync(Guid animalId, EscopoAcessoColmeia escopo, bool permitido, string? rota);

    /// <summary>
    /// Registra o acesso em nome de um veterinário informado explicitamente (RN-067).
    ///
    /// Existe para quem lê fora de uma requisição HTTP. A sobrecarga acima resolve o
    /// "quem" pelo token, e num job não há token: o registro sairia sem ator, e ele é
    /// justamente o que o Responsável consulta na trilha. Quem lê em nome do
    /// veterinário continua sendo o veterinário — mesmo quando quem executa a leitura
    /// é a IA, no job de estruturação.
    /// </summary>
    Task RegistrarAcessoAsync(
        Guid veterinarioId, Guid animalId, EscopoAcessoColmeia escopo, bool permitido, string? rota);

    /// <summary>
    /// Estende a autorização vigente do par animal/veterinário até depois do retorno
    /// (RN-090).
    ///
    /// Não concede nada novo: só o Responsável concede, e essa regra não se dobra por
    /// conveniência do fluxo. O que se evita aqui é o profissional perder o acesso ao
    /// histórico no meio de um tratamento que ele mesmo está conduzindo — sem
    /// autorização vigente, não há o que estender, e o retorno acontece com a visão
    /// restrita.
    ///
    /// Devolve <c>null</c> quando não havia autorização a estender.
    /// </summary>
    Task<AcessoColmeiaDto?> EstenderAsync(Guid animalId, Guid veterinarioId, DateTime ate);

    /// <summary>
    /// Abre o acesso do profissional ao histórico do animal que ele vai atender
    /// (RN-064/RN-090).
    ///
    /// Roda na confirmação do pagamento, sem interação: agendar com um profissional
    /// <b>é</b> autorizá-lo a ler o histórico do animal — exigir um segundo
    /// consentimento explícito para isso levaria o Responsável a chegar na consulta com
    /// o veterinário às cegas, que é o problema que a colmeia existe para resolver.
    ///
    /// Escopo restrito ao atendimento e validade curta: é acesso para atender, não
    /// procuração. Autorização vigente não é duplicada.
    /// </summary>
    Task<AcessoColmeiaDto?> AbrirParaAtendimentoAsync(Guid animalId, Guid veterinarioId, DateTime ate);
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
