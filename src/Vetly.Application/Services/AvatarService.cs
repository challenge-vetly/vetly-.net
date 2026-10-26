using Microsoft.Extensions.Logging;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Comum;
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
    private readonly IHistoricoDoAvatarRepository _historico;
    private readonly ILogger<AvatarService> _logger;
    private readonly GuardaDeLeituraDoAnimal _guarda;

    public AvatarService(
        IAnimalRepository animais,
        IColmeiaService colmeia,
        IUsuarioAtual usuario,
        CalculadoraDoAvatar calculadora,
        IHistoricoDoAvatarRepository historico,
        ILogger<AvatarService> logger)
    {
        _animais = animais;
        _calculadora = calculadora;
        _historico = historico;
        _logger = logger;

        // A mesma guarda do board: quem le o board le o avatar, e ninguem mais
        _guarda = new GuardaDeLeituraDoAnimal(animais, colmeia, usuario, nameof(AvatarService));
    }

    /// <inheritdoc/>
    public async Task<AvatarDoPetDto> ObterAsync(Guid animalId)
    {
        var animal = await ObterNoEscopoAsync(animalId);
        var avatar = await _calculadora.CalcularAsync(animal, DateTime.UtcNow);

        // O historico e consequencia da leitura, nao parte dela: com o banco de
        // documentos fora do ar o Responsavel ainda ve o avatar, e o dia volta a ser
        // gravado pela rotina diaria ou pela proxima leitura
        try
        {
            await _historico.RegistrarAsync(RegistroDiarioDoAvatarDto.De(avatar));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Historico do avatar indisponivel; avatar do animal {AnimalId} servido sem gravar.", animalId);
        }

        return avatar;
    }

    /// <inheritdoc/>
    public async Task<ResultadoPaginado<RegistroDiarioDoAvatarDto>> ObterHistoricoAsync(
        Guid animalId, Paginacao paginacao, Ordenacao? ordenacao = null)
    {
        await ObterNoEscopoAsync(animalId);

        var criterio = (ordenacao ?? new Ordenacao()).Resolver(
            CamposDeOrdenacao.HistoricoDoAvatar.Todos, CamposDeOrdenacao.HistoricoDoAvatar.Data, descendentePadrao: true);

        return await _historico.ObterPaginadoAsync(animalId, paginacao, criterio);
    }

    private async Task<Domain.Entities.Animal> ObterNoEscopoAsync(Guid animalId)
    {
        var animal = await _animais.ObterPorIdAsync(animalId)
            ?? throw new NotFoundException("Animal", animalId);

        await _guarda.GarantirAsync(animal);
        return animal;
    }
}
