using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Representa um documento clínico gerado na plataforma Vetly.
/// Pode estar vinculado a uma consulta ou a uma internação.
/// RN-031: todo documento deve ser assinado digitalmente antes da finalização da consulta.
/// </summary>
public class Documento
{
    /// <summary>Identificador único do documento (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Id da consulta à qual o documento está vinculado.
    /// Nulo quando o documento é gerado a partir de uma internação.
    /// </summary>
    public Guid? ConsultaId { get; private set; }

    /// <summary>
    /// Id da internação à qual o documento está vinculado.
    /// Nulo quando o documento é gerado a partir de uma consulta.
    /// </summary>
    public Guid? InternacaoId { get; private set; }

    /// <summary>Tipo do documento (Prontuário, Receita, Atestado ou Nota Fiscal).</summary>
    [Required]
    public TipoDocumento TipoDocumento { get; private set; }

    /// <summary>
    /// Versão do documento. Começa em 1 e é incrementada a cada correção.
    /// Garante rastreabilidade do histórico de alterações.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int Versao { get; private set; }

    /// <summary>Data e hora de geração do documento.</summary>
    public DateTime DataGeracao { get; private set; }

    /// <summary>CRMV do veterinário que assinou o documento (signatário legal).</summary>
    [Required]
    [MaxLength(15)]
    public string CrmvSignatario { get; private set; }

    /// <summary>
    /// Indica se o documento foi assinado digitalmente.
    /// RN-031: a assinatura é pré-requisito para finalização da consulta.
    /// </summary>
    public bool AssinadoDigitalmente { get; private set; }

    /// <summary>Id do documento original quando este é uma versão corrigida (RN-032).</summary>
    public Guid? VersaoOriginalId { get; private set; }

    /// <summary>Data e hora em que a correção foi realizada (RN-032).</summary>
    public DateTime? DataCorrecao { get; private set; }

    /// <summary>CRMV do veterinário que solicitou a correção (RN-032).</summary>
    [MaxLength(15)]
    public string? CrmvSolicitanteCorrecao { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core para materialização de entidades.</summary>
    private Documento()
    {
        CrmvSignatario = null!;
    }

    /// <summary>
    /// Cria um novo documento associado a uma consulta ou internação.
    /// O documento começa na versão 1 e não assinado.
    /// </summary>
    public Documento(TipoDocumento tipo, string crmvSignatario, Guid? consultaId = null, Guid? internacaoId = null)
    {
        Id = Guid.NewGuid();
        TipoDocumento = tipo;
        CrmvSignatario = crmvSignatario;
        ConsultaId = consultaId;
        InternacaoId = internacaoId;
        Versao = 1;
        DataGeracao = DateTime.UtcNow;
    }

    /// <summary>Registra a assinatura digital do documento (RN-031).</summary>
    public void Assinar() => AssinadoDigitalmente = true;

    /// <summary>Incrementa a versão do documento ao criar uma correção.</summary>
    public void IncrementarVersao() => Versao++;

    /// <summary>
    /// Marca este documento como versão corrigida de outro (RN-032).
    /// Vincula ao documento original e registra o responsável pela correção.
    /// </summary>
    public void Corrigir(Guid versaoOriginalId, DateTime dataCorrecao, string crmvSolicitante)
    {
        VersaoOriginalId = versaoOriginalId;
        DataCorrecao = dataCorrecao;
        CrmvSolicitanteCorrecao = crmvSolicitante;
    }
}
