using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Concessão de acesso ao histórico completo de um animal a um veterinário, por evento
/// clínico (RN-083). Criada automaticamente ao confirmar uma consulta, se o Responsável
/// tem consentimento de compartilhamento na rede ativo. Expira ao fim do ciclo
/// (consulta + 24h — RN-085); retornos vinculados geram sua própria concessão nova, o
/// que renova o acesso na prática sem precisar de lógica extra.
/// </summary>
public class ConcessaoAcessoProntuario
{
    public Guid Id { get; private set; }

    [Required]
    public Guid AnimalId { get; private set; }

    [Required]
    public Guid VeterinarioId { get; private set; }

    [Required]
    public Guid ConsultaId { get; private set; }

    [Required]
    public BaseAcesso BaseAcesso { get; private set; }

    [Required]
    public DateTime ConcedidoEm { get; private set; }

    [Required]
    public DateTime ExpiraEm { get; private set; }

    /// <summary>
    /// Revogação manual, distinta da expiração natural. Não apaga o registro — apenas
    /// bloqueia novos acessos a partir desse momento (RN-087).
    /// </summary>
    public bool Revogada { get; private set; }

    private ConcessaoAcessoProntuario() { }

    public ConcessaoAcessoProntuario(
        Guid animalId, Guid veterinarioId, Guid consultaId, BaseAcesso baseAcesso,
        DateTime concedidoEm, DateTime expiraEm)
    {
        Id = Guid.NewGuid();
        AnimalId = animalId;
        VeterinarioId = veterinarioId;
        ConsultaId = consultaId;
        BaseAcesso = baseAcesso;
        ConcedidoEm = concedidoEm;
        ExpiraEm = expiraEm;
    }

    public void Revogar() => Revogada = true;

    /// <summary>True se ainda não foi revogada nem expirou em relação a <paramref name="agora"/>.</summary>
    public bool EstaAtiva(DateTime agora) => !Revogada && agora <= ExpiraEm;
}
