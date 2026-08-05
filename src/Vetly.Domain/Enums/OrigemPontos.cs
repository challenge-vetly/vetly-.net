namespace Vetly.Domain.Enums;

/// <summary>Evento que originou um lançamento de <c>PontosFidelidade</c> (RN-070).</summary>
public enum OrigemPontos
{
    /// <summary>Obrigação do pet cumprida dentro do prazo, via consulta na Vetly.</summary>
    ObrigacaoCumprida = 1,

    /// <summary>Consulta avulsa (sem obrigação pendente correspondente) — pontua com peso menor.</summary>
    ConsultaAvulsa = 2
}
