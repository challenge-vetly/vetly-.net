namespace Vetly.Domain.Enums;

/// <summary>Status de moderação do comentário de uma avaliação (RN-080).</summary>
public enum StatusModeracao
{
    /// <summary>Comentário publicado normalmente.</summary>
    Publicada = 1,

    /// <summary>
    /// Comentário ocultado por moderação (dados pessoais, ofensas ou conteúdo fora de
    /// escopo). A nota geral nunca é afetada — RN-080 só modera o texto.
    /// </summary>
    OcultaPorModeracao = 2
}
