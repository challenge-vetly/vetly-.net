using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Repasse;

/// <summary>
/// Conta em que o prestador passa a receber o repasse (§4.1, RN-072).
///
/// Substitui a conta inteira, e não campo a campo: metade de uma conta nova com
/// metade da antiga é o erro que manda dinheiro para a pessoa errada, e é caro
/// justamente porque parece ter dado certo.
/// </summary>
public class DefinirDadosDeRepasseDto
{
    /// <summary>Código ou nome do banco (ex.: <c>341</c>, <c>Itaú</c>).</summary>
    [Required(ErrorMessage = "O banco é obrigatório.")]
    [MaxLength(60, ErrorMessage = "O banco deve ter no máximo 60 caracteres.")]
    public string Banco { get; set; } = string.Empty;

    /// <summary>Agência, sem o dígito verificador quando ele for separado.</summary>
    [Required(ErrorMessage = "A agência é obrigatória.")]
    [MaxLength(20, ErrorMessage = "A agência deve ter no máximo 20 caracteres.")]
    public string Agencia { get; set; } = string.Empty;

    /// <summary>Número da conta, com dígito.</summary>
    [Required(ErrorMessage = "A conta é obrigatória.")]
    [MaxLength(30, ErrorMessage = "A conta deve ter no máximo 30 caracteres.")]
    public string Conta { get; set; } = string.Empty;

    /// <summary>
    /// CPF ou CNPJ do titular. Aceita com ou sem pontuação — a API normaliza para
    /// dígitos antes de gravar, porque o mesmo documento escrito de dois jeitos não
    /// pode virar dois cadastros na hora de conferir a titularidade.
    /// </summary>
    [Required(ErrorMessage = "O CPF ou CNPJ do titular é obrigatório.")]
    [MaxLength(20, ErrorMessage = "O documento do titular deve ter no máximo 20 caracteres.")]
    public string DocumentoTitular { get; set; } = string.Empty;

    /// <summary>Chave Pix do titular (e-mail, telefone, documento ou aleatória).</summary>
    [Required(ErrorMessage = "A chave Pix é obrigatória.")]
    [MaxLength(140, ErrorMessage = "A chave Pix deve ter no máximo 140 caracteres.")]
    public string ChavePix { get; set; } = string.Empty;
}

/// <summary>
/// Conta de repasse como a API a devolve (§4.1, §7.3).
///
/// Conta e chave Pix voltam <b>mascaradas</b>, e é deliberado. Quem lê é o próprio
/// titular conferindo o que cadastrou, e para isso os últimos dígitos bastam;
/// devolver o número inteiro transformaria qualquer token vazado, log de resposta ou
/// print de tela em dado bancário completo. O banco, a agência e o documento do
/// titular vão claros porque sozinhos não movimentam nada.
/// </summary>
public class DadosDeRepasseDto
{
    /// <summary>Id do titular — o veterinário ou a empresa que recebe.</summary>
    public Guid TitularId { get; set; }

    /// <summary>
    /// Falso quando o prestador ainda não informou a conta. É o que separa
    /// "onboarding financeiro pendente" de "conta cadastrada", e os demais campos
    /// vêm nulos nesse caso.
    /// </summary>
    public bool Configurado { get; set; }

    public string? Banco { get; set; }
    public string? Agencia { get; set; }

    /// <summary>Conta mascarada, só com os últimos dígitos à mostra.</summary>
    public string? Conta { get; set; }

    /// <summary>CPF ou CNPJ do titular, só dígitos.</summary>
    public string? DocumentoTitular { get; set; }

    /// <summary>Chave Pix mascarada, só com os últimos caracteres à mostra.</summary>
    public string? ChavePix { get; set; }

    /// <summary>Quando a conta foi informada ou atualizada pela última vez.</summary>
    public DateTime? AtualizadoEm { get; set; }
}
