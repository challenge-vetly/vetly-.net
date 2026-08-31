using Vetly.Application.DTOs.Midia;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de mídias (§2.6).</summary>
public interface IMidiaService
{
    /// <summary>Registra a mídia e devolve a URL temporária de upload.</summary>
    Task<UrlDeUploadDto> SolicitarUploadAsync(SolicitarUploadDto dto);

    /// <summary>URL temporária de leitura. Conteúdo clínico nunca vira URL pública (RN-090).</summary>
    Task<UrlDeLeituraDto> ObterUrlDeLeituraAsync(Guid midiaId);

    /// <summary>Marca a mídia como enviada. Chamado pela rota que recebe o arquivo.</summary>
    Task ConfirmarUploadAsync(Guid midiaId, long tamanhoBytes);
}
