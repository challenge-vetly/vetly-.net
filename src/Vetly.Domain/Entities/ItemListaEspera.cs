using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Pedido na lista de espera de um prestador (RN-004/RN-037).
///
/// Existe para o caso em que não há horário: em vez de perder a demanda, a
/// plataforma guarda a intenção e avisa quando abrir vaga.
/// </summary>
public class ItemListaEspera
{
    /// <summary>Tempo de prioridade do primeiro da fila ao ser notificado (RN-037).</summary>
    public static readonly TimeSpan JanelaDePrioridade = TimeSpan.FromMinutes(15);

    /// <summary>Identificador único do pedido (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Responsável que entrou na fila.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>Animal que será atendido.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Veterinário desejado.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Necessidade que motivou a espera.</summary>
    [Required]
    public TipoServico Necessidade { get; private set; }

    /// <summary>Situação na fila.</summary>
    [Required]
    public EstadoListaEspera Estado { get; private set; }

    /// <summary>Momento da entrada na fila — é o que define a ordem (RN-037).</summary>
    public DateTime CriadoEm { get; private set; }

    /// <summary>Horário oferecido quando a vaga abriu.</summary>
    public Guid? SlotOferecidoId { get; private set; }

    /// <summary>Até quando a prioridade sobre a vaga oferecida vale (RN-037).</summary>
    public DateTime? PrioridadeAte { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private ItemListaEspera() { }

    /// <summary>Coloca o Responsável na fila de um veterinário.</summary>
    public ItemListaEspera(Guid tutorId, Guid animalId, Guid veterinarioId, TipoServico necessidade)
    {
        Id = Guid.NewGuid();
        TutorId = tutorId;
        AnimalId = animalId;
        VeterinarioId = veterinarioId;
        Necessidade = necessidade;
        Estado = EstadoListaEspera.Aguardando;
        CriadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Oferece a vaga e abre a janela de prioridade de 15 minutos (RN-037).
    /// </summary>
    public void Notificar(Guid slotId, DateTime agora)
    {
        if (Estado != EstadoListaEspera.Aguardando)
            throw new InvalidOperationException("Só quem está aguardando pode receber a oferta de vaga.");

        Estado = EstadoListaEspera.Notificado;
        SlotOferecidoId = slotId;
        PrioridadeAte = agora.Add(JanelaDePrioridade);
    }

    /// <summary>Verdadeiro enquanto a prioridade sobre a vaga oferecida vale.</summary>
    public bool PrioridadeValida(DateTime agora) =>
        Estado == EstadoListaEspera.Notificado && PrioridadeAte is not null && PrioridadeAte > agora;

    /// <summary>Registra o aceite da vaga — o Responsável seguiu para o checkout.</summary>
    public void Confirmar() => Estado = EstadoListaEspera.Confirmado;

    /// <summary>
    /// Encerra a prioridade sem resposta. A vaga passa ao próximo da fila, e este
    /// pedido sai — a fila não pode ficar presa em quem não respondeu (RN-037).
    /// </summary>
    public void Expirar()
    {
        Estado = EstadoListaEspera.Expirado;
        SlotOferecidoId = null;
        PrioridadeAte = null;
    }

    /// <summary>O Responsável desistiu da espera.</summary>
    public void Cancelar() => Estado = EstadoListaEspera.Cancelado;
}
