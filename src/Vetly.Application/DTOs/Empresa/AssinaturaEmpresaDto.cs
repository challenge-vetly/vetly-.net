namespace Vetly.Application.DTOs.Empresa;

/// <summary>DTO de resposta com o estado atual da assinatura Enterprise da empresa (RN-092).</summary>
public class AssinaturaEmpresaDto
{
    public Guid EmpresaId { get; set; }
    public int QtdVeterinariosAtivos { get; set; }
    public decimal FaixaEnterprise { get; set; }
}
