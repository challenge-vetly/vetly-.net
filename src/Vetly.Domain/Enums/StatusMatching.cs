namespace Vetly.Domain.Enums;

/// <summary>Situação do prestador no motor de busca/matching (RN-030 a RN-033).</summary>
public enum StatusMatching
{
    /// <summary>Elegível a aparecer nos resultados da busca.</summary>
    Ativo = 1,

    /// <summary>Suspenso do matching por decisão da plataforma.</summary>
    Suspenso = 2
}
