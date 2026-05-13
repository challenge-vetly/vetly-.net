namespace Vetly.Application.DTOs.Tutor;

/// <summary>DTO de resposta com os dados de um tutor.</summary>
public class TutorDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public bool ConsentimentoAtendimento { get; set; }
    public bool ConsentimentoLembretes { get; set; }
    public bool ConsentimentoCompartilhamento { get; set; }
    public DateTime? DataConsentimento { get; set; }
    public bool Ativo { get; set; }
}
