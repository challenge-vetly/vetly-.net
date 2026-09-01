using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Exame;
using Vetly.Application.DTOs.Prontuario;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de animais.</summary>
public interface IAnimalService
{
    Task<IEnumerable<AnimalDto>> ObterTodosAsync();
    Task<AnimalDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<ProntuarioDto>> ObterHistoricoAsync(Guid animalId);

    /// <summary>
    /// Board do pet: obrigações, próximos agendamentos, documentos recentes e o estado
    /// do avatar (RN-011/RN-020/RN-090/RN-096).
    /// </summary>
    Task<BoardDoPetDto> ObterBoardAsync(Guid animalId);

    /// <summary>
    /// Registra o peso aferido no atendimento (RN-081). É a única escrita que o
    /// veterinário faz no cadastro do animal, e continua limitada aos que ele atende.
    /// </summary>
    Task<AnimalDto> RegistrarPesoAsync(Guid animalId, decimal pesoKg);

    /// <summary>
    /// Oculta ou volta a exibir um registro do histórico no board do Responsável
    /// (RN-068).
    ///
    /// O registro não é apagado: o profissional continua vendo, e a guarda regulatória
    /// do prontuário permanece. Registro que carrega alerta de segurança não é
    /// ocultável.
    /// </summary>
    Task<ProntuarioDto> DefinirVisibilidadeDoHistoricoAsync(Guid animalId, Guid registroId, bool oculto);
    Task<IEnumerable<ExameDto>> ObterExamesAsync(Guid animalId);
    Task<AnimalDto> CriarAsync(CriarAnimalDto dto);
    Task AtualizarAsync(Guid id, CriarAnimalDto dto);
    Task DesativarAsync(Guid id);
}
