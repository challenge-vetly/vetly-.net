using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Quem pode <b>ler</b> os dados de um animal (RN-105/RN-090): o Admin, o
/// Responsável dono, o veterinário que o atende — e o veterinário de fora quando a
/// colmeia autoriza, com o acesso registrado.
///
/// Saiu de dentro do <see cref="AnimalService"/> para que o avatar aplique
/// exatamente a mesma regra do board. Duas cópias de uma regra de acesso a dado de
/// saúde divergem na primeira manutenção — e a que ficar para trás vira o buraco.
/// </summary>
internal sealed class GuardaDeLeituraDoAnimal
{
    private readonly IAnimalRepository _repo;
    private readonly IColmeiaService _colmeia;
    private readonly IUsuarioAtual _usuario;
    private readonly string _origem;

    /// <param name="repo">Repositório de animais, para o vínculo com o veterinário.</param>
    /// <param name="colmeia">Autorização e trilha da colmeia (RN-090).</param>
    /// <param name="usuario">Perfil do token.</param>
    /// <param name="origem">Quem pediu o acesso, gravado na trilha da colmeia.</param>
    public GuardaDeLeituraDoAnimal(IAnimalRepository repo, IColmeiaService colmeia, IUsuarioAtual usuario, string origem)
    {
        _repo = repo;
        _colmeia = colmeia;
        _usuario = usuario;
        _origem = origem;
    }

    /// <summary>Recusa a leitura de animal fora do escopo (RN-105).</summary>
    /// <exception cref="AcessoNegadoException">Fora do escopo e sem autorização da colmeia.</exception>
    public async Task GarantirAsync(Animal animal)
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
                animal.Id, EscopoAcessoColmeia.HistoricoCompleto, autorizado, _origem);

            if (autorizado)
                return;
        }

        throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");
    }
}
