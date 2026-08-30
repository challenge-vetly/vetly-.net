using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>
/// Pedido de checkout: trava o horário por 10 minutos e cria a consulta em
/// <c>EmCheckout</c>, aguardando o pagamento (RN-003/RN-035/RN-039).
/// </summary>
public class CheckoutDto
{
    [Required(ErrorMessage = "O animal é obrigatório.")]
    public Guid AnimalId { get; set; }

    /// <summary>
    /// Prestador escolhido na busca: o veterinário autônomo ou a clínica. Na clínica,
    /// o profissional é o dono do horário escolhido (RN-003).
    /// </summary>
    [Required(ErrorMessage = "O prestador é obrigatório.")]
    public Guid PrestadorId { get; set; }

    /// <summary>Horário escolhido na disponibilidade.</summary>
    [Required(ErrorMessage = "O horário é obrigatório.")]
    public Guid SlotId { get; set; }

    /// <summary>Serviço contratado, que define o valor (RN-032).</summary>
    [Required(ErrorMessage = "O serviço é obrigatório.")]
    public Guid ServicoId { get; set; }
}

/// <summary>
/// Política de reembolso da clínica, exibida ao Responsável no momento do
/// agendamento (RN-014/RN-042). Transparência antes de cobrar, não depois.
/// </summary>
public class PoliticaDeReembolsoDto
{
    /// <summary>Acima desta antecedência, em horas, o reembolso é integral.</summary>
    public int IntegralAcimaDeHoras { get; set; } = 24;

    /// <summary>Abaixo desta antecedência, em horas, não há reembolso.</summary>
    public int SemReembolsoAbaixoDeHoras { get; set; } = 2;

    /// <summary>Percentual retido na faixa parcial, configurado pela clínica (RN-042).</summary>
    public decimal PercentualRetencaoParcial { get; set; }
}

/// <summary>Resumo do que está sendo agendado, para a tela de confirmação.</summary>
public class ResumoDoCheckoutDto
{
    public string Prestador { get; set; } = string.Empty;
    public TipoServico Servico { get; set; }
    public DateTime DataHora { get; set; }
    public decimal Valor { get; set; }
    public ModalidadeAtendimento Modalidade { get; set; }
    public PoliticaDeReembolsoDto PoliticaReembolso { get; set; } = new();
}

/// <summary>Resposta do checkout: a consulta reservada e até quando o lock vale.</summary>
public class CheckoutCriadoDto
{
    public Guid ConsultaId { get; set; }

    public StatusConsulta Status { get; set; }

    /// <summary>
    /// Instante em que a reserva do horário expira. Passado ele, o horário volta a
    /// valer para outro Responsável (RN-035).
    /// </summary>
    public DateTime LockExpiraEm { get; set; }

    public ResumoDoCheckoutDto Resumo { get; set; } = new();
}
