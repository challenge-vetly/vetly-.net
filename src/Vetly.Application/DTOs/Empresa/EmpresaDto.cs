using Vetly.Application.DTOs.Comum;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Empresa;

/// <summary>DTO de resposta com os dados de uma empresa.</summary>
public class EmpresaDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public Guid AdministradorId { get; set; }
    public bool Ativa { get; set; }

    /// <summary>Endereço da unidade, com a coordenada derivada (RN-026).</summary>
    public EnderecoDto? Endereco { get; set; }

    /// <summary>Percentual retido no cancelamento da faixa parcial (RN-042).</summary>
    public decimal PercentualRetencaoParcial { get; set; }

    /// <summary>Plano de assinatura da unidade (RN-070/RN-072).</summary>
    public PlanoAssinatura Plano { get; set; }

    /// <summary>Faixa Enterprise vigente, derivada do número de vets vinculados (RN-072).</summary>
    public FaixaEnterprise? FaixaEnterprise { get; set; }
}
