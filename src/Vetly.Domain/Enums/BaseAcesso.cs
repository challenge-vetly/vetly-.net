namespace Vetly.Domain.Enums;

/// <summary>Base jurídica/funcional de um acesso ao prontuário do animal (RN-010/083/085).</summary>
public enum BaseAcesso
{
    /// <summary>Acesso concedido pela colmeia por evento clínico (consentimento de rede ativo — RN-083).</summary>
    ConsentimentoRede = 1,

    /// <summary>Acesso restrito clássico: só ao que o próprio veterinário produziu (RN-010).</summary>
    AtendimentoDireto = 2
}
