using Vetly.Application.DTOs.Dispositivo;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de dispositivos do Responsável, base do push (RN-007/RN-092).
/// </summary>
public class DispositivoService : IDispositivoService
{
    private readonly IDispositivoRepository _repo;
    private readonly ITutorRepository _tutorRepo;
    private readonly IUsuarioAtual _usuario;

    public DispositivoService(
        IDispositivoRepository repo, ITutorRepository tutorRepo, IUsuarioAtual usuario)
    {
        _repo = repo;
        _tutorRepo = tutorRepo;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DispositivoDto>> ObterDoTutorAsync(Guid tutorId)
    {
        GarantirPosse(tutorId);

        var dispositivos = await _repo.ObterAtivosDoTutorAsync(tutorId);
        return dispositivos.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task<DispositivoDto> RegistrarAsync(Guid tutorId, RegistrarDispositivoDto dto)
    {
        GarantirPosse(tutorId);

        _ = await _tutorRepo.ObterPorIdAsync(tutorId)
            ?? throw new NotFoundException("Tutor", tutorId);

        // Reinstalar o app devolve o mesmo push token do fabricante. Reaproveitar o
        // registro evita duplicata e mantem o historico de entrega.
        var existente = await _repo.ObterPorPushTokenAsync(dto.PushToken);

        if (existente is not null)
        {
            if (existente.TutorId != tutorId)
            {
                // O aparelho trocou de dono: o registro antigo sai de circulacao para
                // que o push do Responsavel anterior nao caia no aparelho de outro.
                existente.Desativar();
                _repo.Atualizar(existente);
                await _repo.SalvarAsync();
            }
            else
            {
                existente.Reativar(DateTime.UtcNow);
                _repo.Atualizar(existente);
                await _repo.SalvarAsync();
                return MapearParaDto(existente);
            }
        }

        var dispositivo = new Dispositivo(tutorId, dto.PushToken, dto.Plataforma);
        await _repo.AdicionarAsync(dispositivo);
        await _repo.SalvarAsync();

        return MapearParaDto(dispositivo);
    }

    /// <inheritdoc/>
    public async Task RemoverAsync(Guid tutorId, Guid dispositivoId)
    {
        GarantirPosse(tutorId);

        var dispositivo = await _repo.ObterPorIdAsync(dispositivoId)
            ?? throw new NotFoundException("Dispositivo", dispositivoId);

        if (dispositivo.TutorId != tutorId)
            throw new AcessoNegadoException("RN-105", "Este dispositivo nao pertence ao seu cadastro.");

        // Remocao logica: o historico de entrega de push depende do registro
        dispositivo.Desativar();
        _repo.Atualizar(dispositivo);
        await _repo.SalvarAsync();
    }

    /// <summary>Recusa operar dispositivos de outro Responsável (RN-105).</summary>
    private void GarantirPosse(Guid tutorId)
    {
        if (_usuario.EhAdmin || _usuario.TutorId == tutorId)
            return;

        throw new AcessoNegadoException("RN-105", "Este cadastro nao pertence ao seu escopo de acesso.");
    }

    /// <summary>
    /// O push token inteiro nao volta ao cliente: ele nao precisa dele de volta, e
    /// devolve-lo so aumentaria a superficie de vazamento.
    /// </summary>
    private static DispositivoDto MapearParaDto(Dispositivo d) => new()
    {
        Id = d.Id,
        TutorId = d.TutorId,
        PushToken = d.PushToken.Length <= 8 ? "***" : $"***{d.PushToken[^6..]}",
        Plataforma = d.Plataforma,
        RegistradoEm = d.RegistradoEm,
        UltimoUsoEm = d.UltimoUsoEm,
        Ativo = d.Ativo
    };
}
