using System.ComponentModel.DataAnnotations;
using Vetly.Application.DTOs.Comum;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Empresa;

/// <summary>DTO de entrada para cadastro de uma nova empresa.</summary>
public class CriarEmpresaDto
{
    [Required(ErrorMessage = "O nome da empresa é obrigatório.")]
    [MaxLength(300)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "O tipo da empresa é obrigatório.")]
    [MaxLength(100)]
    public string Tipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "O id do administrador é obrigatório.")]
    public Guid AdministradorId { get; set; }

    /// <summary>Endereço da unidade (RN-026). A coordenada é derivada dele, não informada.</summary>
    public EnderecoDto? Endereco { get; set; }

    /// <summary>
    /// Percentual retido no cancelamento entre 24h e 2h da consulta (RN-042).
    /// Quando omitido, vale o padrão de 30% do onboarding.
    /// </summary>
    [Range(0, 100, ErrorMessage = "O percentual de retenção deve estar entre 0 e 100.")]
    public decimal? PercentualRetencaoParcial { get; set; }

    /// <summary>Plano de assinatura da unidade, que define o take rate (RN-070/RN-072).</summary>
    public PlanoAssinatura? Plano { get; set; }
}
