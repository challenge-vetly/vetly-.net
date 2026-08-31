namespace Vetly.Domain.Enums;

/// <summary>
/// Até onde vai a autorização que o Responsável concede na colmeia (RN-090).
///
/// Existe granularidade porque "compartilhar o histórico" quase nunca quer dizer
/// tudo: pedir segunda opinião sobre um exame não é o mesmo que abrir o prontuário
/// inteiro do animal desde filhote.
/// </summary>
public enum EscopoAcessoColmeia
{
    /// <summary>Todo o histórico clínico do animal.</summary>
    HistoricoCompleto = 1,

    /// <summary>Apenas o último atendimento — o caso da segunda opinião.</summary>
    UltimaConsulta = 2,

    /// <summary>Apenas os documentos publicados no board do pet.</summary>
    Documentos = 3
}
