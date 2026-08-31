namespace Vetly.Domain.Enums;

/// <summary>
/// Natureza do arquivo guardado no storage de objetos (§2.6, §11).
/// Define a retenção: áudio de consulta some em 30 dias (P-06), documento clínico fica.
/// </summary>
public enum TipoMidia
{
    /// <summary>Foto do pet no cadastro.</summary>
    FotoPet = 1,

    /// <summary>Mídia anexada aos pré-sintomas pelo Responsável (RN-036).</summary>
    PreSintoma = 2,

    /// <summary>Segmento de áudio da consulta (RN-009/RN-079).</summary>
    AudioConsulta = 3,

    /// <summary>Resultado de exame em PDF ou imagem (RN-104).</summary>
    ResultadoExame = 4,

    /// <summary>PDF de documento clínico gerado (RN-090).</summary>
    DocumentoPdf = 5
}
