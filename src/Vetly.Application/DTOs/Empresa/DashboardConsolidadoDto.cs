namespace Vetly.Application.DTOs.Empresa;

/// <summary>
/// DTO de resposta do dashboard financeiro consolidado da empresa (RN-007). Por
/// construção, nunca inclui dados bancários pessoais de vets, remuneração interna
/// individual ou dados de outras empresas — não é um filtro aplicado depois, é a lista
/// de campos que o DTO tem.
/// </summary>
public class DashboardConsolidadoDto
{
    public Guid EmpresaId { get; set; }
    public int QtdVeterinariosAtivos { get; set; }
    public decimal FaixaEnterprise { get; set; }

    /// <summary>Soma do valor bruto de todos os pagamentos confirmados/parciais/estornados dos vets da empresa.</summary>
    public decimal FaturamentoBruto { get; set; }

    /// <summary>Soma das comissões retidas pela plataforma (RN-089).</summary>
    public decimal TotalComissoes { get; set; }

    /// <summary>Soma dos valores de repasse aos veterinários (RN-089).</summary>
    public decimal TotalRepasses { get; set; }

    /// <summary>Soma dos valores estornados por cancelamento (RN-019..021).</summary>
    public decimal TotalReembolsos { get; set; }

    public int QtdConsultasRealizadas { get; set; }
    public int QtdConsultasCanceladas { get; set; }
}
