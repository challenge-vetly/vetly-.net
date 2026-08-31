namespace Vetly.Domain.Enums;

/// <summary>
/// Decisão do veterinário sobre o rascunho produzido pela IA (RN-082, §7.3).
///
/// São três caminhos, e a diferença entre eles importa: aprovar sem ler e corrigir
/// antes de aprovar não podem ficar registrados da mesma forma. Não aprovar é um
/// desfecho legítimo — o ciclo encerra sem documentos, e a consulta segue pelo
/// prontuário manual.
/// </summary>
public enum DecisaoSobreRascunho
{
    /// <summary>Rascunho gerado, ainda sem decisão do veterinário.</summary>
    Pendente = 1,

    /// <summary>Aprovado como veio da IA, sem alteração.</summary>
    Aprovado = 2,

    /// <summary>Corrigido pelo veterinário antes de aprovar.</summary>
    Corrigido = 3,

    /// <summary>Recusado: o ciclo encerra sem documentos.</summary>
    NaoAprovado = 4,

    /// <summary>Prontuário escrito à mão, sem IA no caminho (RN-085).</summary>
    Manual = 5
}
