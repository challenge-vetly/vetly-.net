using Vetly.Application.DTOs.Midia;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Registro e acesso às mídias do storage de objetos (§2.6).
///
/// A API nunca carrega bytes: registra a mídia, entrega URL temporária e o app fala
/// direto com o storage.
/// </summary>
public class MidiaService : IMidiaService
{
    /// <summary>Tipos MIME aceitos por tipo de mídia.</summary>
    private static readonly Dictionary<TipoMidia, string[]> TiposAceitos = new()
    {
        [TipoMidia.FotoPet] = ["image/jpeg", "image/png", "image/webp"],
        [TipoMidia.PreSintoma] = ["image/jpeg", "image/png", "image/webp", "video/mp4"],
        // OGG-OPUS e WAV/PCM sao o que o motor de transcricao le (§5.3). WebM continua
        // aceito no upload — segmento em formato que o motor recusa falha com
        // FormatoNaoSuportado, e o app antigo nao fica sem conseguir nem enviar.
        [TipoMidia.AudioConsulta] =
        [
            "audio/ogg", "audio/ogg;codecs=opus", "audio/ogg; codecs=opus",
            "audio/wav", "audio/wave", "audio/x-wav",
            "audio/webm", "audio/webm;codecs=opus", "audio/mpeg"
        ],
        [TipoMidia.ResultadoExame] = ["application/pdf", "image/jpeg", "image/png"],
        [TipoMidia.DocumentoPdf] = ["application/pdf"]
    };

    private readonly IMidiaRepository _repo;
    private readonly IStorageAdapter _storage;
    private readonly IUsuarioAtual _usuario;

    public MidiaService(IMidiaRepository repo, IStorageAdapter storage, IUsuarioAtual usuario)
    {
        _repo = repo;
        _storage = storage;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<UrlDeUploadDto> SolicitarUploadAsync(SolicitarUploadDto dto)
    {
        // Content type conferido no registro, nao so no upload: aceitar qualquer coisa
        // aqui deixaria o storage virar deposito de arquivo arbitrario.
        if (!TiposAceitos.TryGetValue(dto.Tipo, out var aceitos) ||
            !aceitos.Contains(dto.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("contentType",
                $"O tipo '{dto.ContentType}' nao e aceito para midia do tipo {dto.Tipo}.");
        }

        var midia = new Midia(dto.Tipo, dto.ContentType, _usuario.TutorId, dto.ConsultaId);

        await _repo.AdicionarAsync(midia);
        await _repo.SalvarAsync();

        var url = await _storage.GerarUrlDeUploadAsync(midia.ChaveStorage, midia.ContentType, Midia.ValidadeDaUrl);

        return new UrlDeUploadDto
        {
            MidiaId = midia.Id,
            UploadUrl = url.Url,
            ExpiraEm = url.ExpiraEm,
            ContentType = midia.ContentType
        };
    }

    /// <inheritdoc/>
    public async Task<UrlDeLeituraDto> ObterUrlDeLeituraAsync(Guid midiaId)
    {
        var midia = await _repo.ObterPorIdAsync(midiaId)
            ?? throw new NotFoundException("Midia", midiaId);

        GarantirAcesso(midia);

        if (midia.Status == StatusMidia.Removida)
            throw new BusinessRuleException("MIDIA-002", "Este arquivo nao esta mais disponivel.");

        // O upload pode ter sido registrado e nunca concluido: confere no storage antes
        // de entregar uma URL que nao levaria a lugar nenhum.
        if (!midia.Disponivel())
        {
            var tamanho = await _storage.ObterTamanhoAsync(midia.ChaveStorage);

            if (tamanho is null or 0)
                throw new BusinessRuleException("MIDIA-001", "O arquivo ainda nao foi enviado.");

            midia.ConfirmarUpload(tamanho.Value);
            _repo.Atualizar(midia);
            await _repo.SalvarAsync();
        }

        var url = await _storage.GerarUrlDeLeituraAsync(midia.ChaveStorage, Midia.ValidadeDaUrl);

        return new UrlDeLeituraDto
        {
            MidiaId = midia.Id,
            Url = url.Url,
            ExpiraEm = url.ExpiraEm,
            ContentType = midia.ContentType,
            TamanhoBytes = midia.TamanhoBytes
        };
    }

    /// <inheritdoc/>
    public async Task ConfirmarUploadAsync(Guid midiaId, long tamanhoBytes)
    {
        var midia = await _repo.ObterPorIdAsync(midiaId)
            ?? throw new NotFoundException("Midia", midiaId);

        midia.ConfirmarUpload(tamanhoBytes);
        _repo.Atualizar(midia);
        await _repo.SalvarAsync();
    }

    /// <summary>
    /// Conteúdo clínico não vira URL aberta (RN-090): a mídia do Responsável é dele,
    /// e o vet alcança a que pertence a uma consulta dele.
    /// </summary>
    private void GarantirAcesso(Midia midia)
    {
        if (_usuario.EhAdmin || _usuario.EhVeterinario)
            return;

        if (_usuario.EhTutor && _usuario.TutorId == midia.TutorId)
            return;

        throw new AcessoNegadoException("RN-090", "Este arquivo nao pertence ao seu escopo de acesso.");
    }
}
