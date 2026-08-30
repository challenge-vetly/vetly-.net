namespace Vetly.Application.DTOs.Veterinario;

/// <summary>
/// Resposta do cadastro de veterinário: os dados do perfil mais a <b>senha temporária</b>
/// gerada para o primeiro acesso.
///
/// A senha aparece <b>uma única vez</b>, nesta resposta. Não é persistida em claro nem
/// volta em nenhuma outra rota — o Admin precisa repassá-la ao profissional, que a troca
/// no primeiro acesso. É o caminho conservador da pendência P-05, já que o projeto não
/// tem serviço de e-mail para enviar convite.
/// </summary>
public class VeterinarioCriadoDto
{
    /// <summary>Perfil recém-criado.</summary>
    public VeterinarioDto Veterinario { get; set; } = new();

    /// <summary>Senha temporária de primeiro acesso. Exibida somente nesta resposta.</summary>
    public string SenhaTemporaria { get; set; } = string.Empty;

    /// <summary>E-mail de acesso do profissional.</summary>
    public string Email { get; set; } = string.Empty;
}
