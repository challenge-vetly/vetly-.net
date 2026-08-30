using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Um horário materializado da agenda de um veterinário (RN-034/RN-035).
///
/// O slot é o que impede overbooking sem gateway real: o checkout trava o horário por
/// 10 minutos, e só a confirmação do pagamento o ocupa de vez.
/// </summary>
public class Slot
{
    /// <summary>Duração do lock de checkout (RN-035).</summary>
    public static readonly TimeSpan DuracaoDoLock = TimeSpan.FromMinutes(10);

    /// <summary>Identificador único do slot (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Veterinário dono do horário.</summary>
    [Required]
    public Guid VeterinarioId { get; private set; }

    /// <summary>Início do horário (UTC).</summary>
    [Required]
    public DateTime Inicio { get; private set; }

    /// <summary>Fim do horário (UTC).</summary>
    [Required]
    public DateTime Fim { get; private set; }

    /// <summary>Estado do horário na máquina da RN-035.</summary>
    [Required]
    public EstadoSlot Estado { get; private set; }

    /// <summary>
    /// Até quando o lock de checkout vale. Nulo fora do checkout.
    /// Passado esse instante o slot volta a valer como livre, mesmo antes do job
    /// de expiração rodar — a condição é avaliada na leitura.
    /// </summary>
    public DateTime? LockAte { get; private set; }

    /// <summary>Consulta que segura o lock ou ocupa o horário.</summary>
    public Guid? LockConsultaId { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Slot() { }

    /// <summary>Materializa um horário livre da agenda.</summary>
    public Slot(Guid veterinarioId, DateTime inicio, DateTime fim)
    {
        if (fim <= inicio)
            throw new ArgumentException("O fim do horário deve ser depois do início.", nameof(fim));

        Id = Guid.NewGuid();
        VeterinarioId = veterinarioId;
        Inicio = inicio;
        Fim = fim;
        Estado = EstadoSlot.Livre;
    }

    /// <summary>
    /// Verdadeiro quando o horário pode ser tomado no instante informado: livre, ou
    /// em checkout com o lock já vencido.
    /// </summary>
    public bool EstaDisponivel(DateTime agora) =>
        Estado == EstadoSlot.Livre ||
        (Estado == EstadoSlot.EmCheckout && LockAte is not null && LockAte < agora);

    /// <summary>
    /// Trava o horário para o checkout de uma consulta (RN-035).
    /// Devolve <c>false</c> quando outro checkout chegou primeiro — o chamador
    /// responde 409 e o Responsável escolhe outro horário.
    /// </summary>
    public bool TravarParaCheckout(Guid consultaId, DateTime agora)
    {
        if (!EstaDisponivel(agora))
            return false;

        Estado = EstadoSlot.EmCheckout;
        LockAte = agora.Add(DuracaoDoLock);
        LockConsultaId = consultaId;
        return true;
    }

    /// <summary>Ocupa o horário em definitivo, na confirmação do pagamento (RN-006/RN-035).</summary>
    public void Confirmar()
    {
        Estado = EstadoSlot.Confirmado;
        LockAte = null;
    }

    /// <summary>
    /// Devolve o horário à disponibilidade — lock expirado, pagamento recusado,
    /// cancelamento ou remarcação. Toda entrada em livre deve disparar a promoção da
    /// lista de espera (RN-037).
    /// </summary>
    public void Liberar()
    {
        Estado = EstadoSlot.Livre;
        LockAte = null;
        LockConsultaId = null;
    }

    /// <summary>Bloqueia o horário por decisão do veterinário (folga, compromisso).</summary>
    public void Bloquear()
    {
        if (Estado == EstadoSlot.Confirmado)
            throw new InvalidOperationException("Não é possível bloquear um horário já confirmado.");

        Estado = EstadoSlot.Bloqueado;
        LockAte = null;
        LockConsultaId = null;
    }
}
