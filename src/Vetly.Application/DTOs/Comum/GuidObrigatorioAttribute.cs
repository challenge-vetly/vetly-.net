using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Comum;

/// <summary>
/// Exige um <see cref="Guid"/> de verdade, recusando <see cref="Guid.Empty"/>.
///
/// <para>
/// <b>Por que <c>[Required]</c> não basta.</b> Em cima de um tipo-valor não anulável,
/// <c>[Required]</c> é praticamente inócuo: o model binder do ASP.NET Core não tem como
/// distinguir "não enviado" de "enviado como zero", então o campo ausente vira
/// <c>Guid.Empty</c>, passa na validação — porque <c>Guid.Empty</c> não é nulo — e
/// segue para o serviço.
/// </para>
/// <para>
/// O sintoma não é um 400 dizendo o que faltou; é um 404 dizendo que o registro
/// <c>00000000-0000-0000-0000-000000000000</c> não existe. A mensagem de erro que o
/// desenvolvedor escreveu no <c>[Required]</c> nunca chega a ser exibida, e quem
/// integra perde tempo procurando um id que ele nunca mandou.
/// </para>
/// <para>
/// <b>Onde aplicar.</b> Em id que é chave de busca e <b>não</b> tem alternativa no
/// servidor. Onde o serviço deriva o valor de outra fonte — o <c>tutorId</c> da
/// cobrança, por exemplo, que vem da claim do token quando quem chama é o Responsável
/// (RN-106) — o campo ausente é legítimo, e exigi-lo quebraria um caminho que funciona.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class GuidObrigatorioAttribute : ValidationAttribute
{
    /// <inheritdoc/>
    public override bool IsValid(object? value) => value switch
    {
        // Nulo fica para o [Required] decidir: acumular as duas responsabilidades aqui
        // produziria duas mensagens para a mesma ausência.
        null => true,
        Guid guid => guid != Guid.Empty,
        _ => true
    };
}
