using Vetly.Application.DTOs.Animal;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;

namespace Vetly.Application.Services;

/// <summary>
/// Avatar do pet (RN-112 a RN-114).
///
/// <para>
/// <b>Sem push próprio (RN-114).</b> O avatar muda porque uma obrigação venceu, e a
/// obrigação vencida já dispara a régua de lembretes (§6.3) com até três tentativas.
/// Um push "seu pet ficou triste" por cima seria o quarto aviso pelo mesmo fato — e
/// notificação repetida é a que o Responsável desliga.
/// </para>
/// </summary>
public class AvatarService : IAvatarService
{
    private readonly IAnimalRepository _animais;
    private readonly CalculadoraDoAvatar _calculadora;
    private readonly GuardaDeLeituraDoAnimal _guarda;

    public AvatarService(
        IAnimalRepository animais,
        IColmeiaService colmeia,
        IUsuarioAtual usuario,
        CalculadoraDoAvatar calculadora)
    {
        _animais = animais;
        _calculadora = calculadora;

        // A mesma guarda do board: quem le o board le o avatar, e ninguem mais
        _guarda = new GuardaDeLeituraDoAnimal(animais, colmeia, usuario, nameof(AvatarService));
    }

    /// <inheritdoc/>
    public async Task<AvatarDoPetDto> ObterAsync(Guid animalId)
    {
        var animal = await _animais.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        await _guarda.GarantirAsync(animal);

        return await _calculadora.CalcularAsync(animal, DateTime.UtcNow);
    }
}
