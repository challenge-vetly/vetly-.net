namespace Vetly.Application.DTOs.Prontuario;

/// <summary>DTO de resposta com os dados de um prontuário clínico.</summary>
public class ProntuarioDto
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }
    public Guid AnimalId { get; set; }
    public string DadosClinicos { get; set; } = string.Empty;
    public Guid? VersaoOriginalId { get; set; }
    public DateTime? DataCorrecao { get; set; }
    public string? JustificativaCorrecao { get; set; }
    public string? CrmvSolicitanteCorrecao { get; set; }
    public DateTime DataCriacao { get; set; }
    public bool ExigeJustificativa { get; set; }

    /// <summary>
    /// Oculto do board do Responsável (RN-068). O registro continua existindo, e o
    /// veterinário continua vendo — o que muda é a tela de quem cuida do animal.
    /// </summary>
    public bool Oculto { get; set; }
}
