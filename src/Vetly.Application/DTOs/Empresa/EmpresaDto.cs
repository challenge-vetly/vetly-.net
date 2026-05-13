namespace Vetly.Application.DTOs.Empresa;

/// <summary>DTO de resposta com os dados de uma empresa.</summary>
public class EmpresaDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public Guid AdministradorId { get; set; }
    public bool Ativa { get; set; }
}
