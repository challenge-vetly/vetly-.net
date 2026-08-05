using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Obrigacao;

/// <summary>DTO de resposta com os dados de uma obrigação do calendário de cuidado do pet.</summary>
public class ObrigacaoDoPetDto
{
    public Guid Id { get; set; }
    public Guid AnimalId { get; set; }
    public TipoObrigacao Tipo { get; set; }
    public DateTime DataLimite { get; set; }
    public StatusObrigacao Status { get; set; }
    public Guid? ConsultaId { get; set; }
    public DateTime? DataCumprimento { get; set; }

    /// <summary>True quando ainda pendente e já passou da data-limite ("atrasada" — não é um status persistido).</summary>
    public bool Atrasada { get; set; }
}
