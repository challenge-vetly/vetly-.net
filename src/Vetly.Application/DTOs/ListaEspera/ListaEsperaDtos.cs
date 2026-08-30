using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.ListaEspera;

/// <summary>Pedido de entrada na lista de espera de um veterinário (RN-004).</summary>
public class EntrarNaListaDto
{
    [Required(ErrorMessage = "O animal é obrigatório.")]
    public Guid AnimalId { get; set; }

    [Required(ErrorMessage = "O veterinário é obrigatório.")]
    public Guid VeterinarioId { get; set; }

    [Required(ErrorMessage = "A necessidade é obrigatória.")]
    public TipoServico Necessidade { get; set; }
}

/// <summary>Confirmação da vaga oferecida (RN-037).</summary>
public class ConfirmarVagaDto
{
    /// <summary>Serviço contratado, que define o valor da consulta (RN-032).</summary>
    [Required(ErrorMessage = "O serviço é obrigatório.")]
    public Guid ServicoId { get; set; }
}

/// <summary>Um pedido na lista de espera.</summary>
public class ItemListaEsperaDto
{
    public Guid Id { get; set; }
    public Guid TutorId { get; set; }
    public Guid AnimalId { get; set; }
    public Guid VeterinarioId { get; set; }
    public TipoServico Necessidade { get; set; }
    public EstadoListaEspera Estado { get; set; }
    public DateTime CriadoEm { get; set; }

    /// <summary>Horário oferecido, quando a vaga abriu.</summary>
    public Guid? SlotOferecidoId { get; set; }

    /// <summary>Data e hora do horário oferecido.</summary>
    public DateTime? HorarioOferecido { get; set; }

    /// <summary>Até quando a prioridade sobre a vaga vale (RN-037).</summary>
    public DateTime? PrioridadeAte { get; set; }
}
