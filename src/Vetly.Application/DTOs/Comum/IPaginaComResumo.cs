namespace Vetly.Application.DTOs.Comum;

/// <summary>
/// Página que carrega, além dos itens, um resumo do filtro inteiro (totais,
/// contagens). É o que permite ao envelope de HATEOAS levar o resumo junto da
/// navegação, sem cada listagem precisar de um envelope próprio.
/// </summary>
public interface IPaginaComResumo
{
    /// <summary>O resumo, serializado como está.</summary>
    object Resumo { get; }
}
