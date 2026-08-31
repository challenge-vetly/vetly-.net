namespace Vetly.Domain.Enums;

/// <summary>Situação de um arquivo no storage (§2.6).</summary>
public enum StatusMidia
{
    /// <summary>
    /// Registro criado, URL de upload entregue, arquivo ainda não enviado.
    /// Mídia que fica aqui para sempre é upload que o app começou e não terminou.
    /// </summary>
    AguardandoUpload = 1,

    /// <summary>Arquivo no storage, pronto para uso.</summary>
    Disponivel = 2,

    /// <summary>Removido do storage — retenção vencida ou exclusão explícita.</summary>
    Removida = 3
}
