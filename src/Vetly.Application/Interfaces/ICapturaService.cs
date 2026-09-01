using Vetly.Application.DTOs.Captura;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato da captura de áudio da consulta (RN-008/RN-009/RN-079/RN-085).
/// </summary>
public interface ICapturaService
{
    /// <summary>Abre a janela de captura (RN-008). No plano Básico, inicia sem captura (RN-085).</summary>
    Task<SessaoIniciadaDto> IniciarAsync(Guid consultaId);

    /// <summary>Recebe um trecho de áudio e enfileira a transcrição (RN-009).</summary>
    Task<SegmentoRecebidoDto> ReceberSegmentoAsync(Guid consultaId, EnviarSegmentoDto dto);

    /// <summary>Situação da captura, com o texto parcial já transcrito.</summary>
    Task<EstadoDaCapturaDto> ObterEstadoAsync(Guid consultaId);

    /// <summary>Fecha a janela e marca a consulta como realizada (RN-008/RN-038).</summary>
    Task<ConsultaEncerradaDto> EncerrarAsync(Guid consultaId);

    /// <summary>Registra o texto devolvido pelo motor de transcrição (§5.3).</summary>
    Task RegistrarCallbackAsync(CallbackDeTranscricaoDto dto);

    /// <summary>
    /// Resolve os segmentos cujo callback nunca voltou: reenvia enquanto houver
    /// tentativa e dá o trecho como perdido quando não houver (§4.2).
    ///
    /// Devolve quantos segmentos foram tratados. Sem esta varredura, motor que aceita
    /// o despacho e depois morre calado prende a sessão em
    /// <c>AguardandoTranscricao</c> para sempre — e o app, que faz polling do
    /// rascunho, nunca chega a um estado terminal.
    /// </summary>
    Task<int> ResolverSegmentosTravadosAsync();
}
