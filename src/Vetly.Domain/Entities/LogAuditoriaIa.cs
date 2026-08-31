using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Registro imutável da decisão do veterinário sobre conteúdo produzido pela IA
/// (RN-082, §7.3).
///
/// A tabela é <b>append-only</b>: não há update nem delete, e não existe método que
/// altere uma linha depois de gravada. É o que sustenta a afirmação de que toda
/// sugestão que chegou ao prontuário passou por decisão humana — um registro que
/// pode ser reescrito depois não prova nada.
///
/// Guarda o conteúdo final tal como o veterinário aceitou, e não um diff: reconstruir
/// o que foi assinado a partir de diferenças é frágil justamente quando mais importa.
/// </summary>
public class LogAuditoriaIa
{
    /// <summary>Identificador do registro (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Consulta a que a decisão se refere.</summary>
    [Required]
    public Guid ConsultaId { get; private set; }

    /// <summary>Sessão de captura, quando houve captura.</summary>
    public Guid? SessaoCapturaId { get; private set; }

    /// <summary>Rascunho decidido. Nulo no prontuário manual, que não teve IA.</summary>
    public Guid? RascunhoIaId { get; private set; }

    /// <summary>Quem decidiu. É a assinatura da responsabilidade clínica (RN-082).</summary>
    public Guid? VeterinarioId { get; private set; }

    /// <summary>Qual dos três caminhos foi tomado.</summary>
    [Required]
    public DecisaoSobreRascunho Decisao { get; private set; }

    /// <summary>
    /// Conteúdo final, como o veterinário aceitou, em JSON. No caso de recusa fica
    /// vazio: não houve conteúdo aceito.
    /// </summary>
    public string ConteudoFinal { get; private set; }

    /// <summary>Motivo da recusa ou da correção, quando informado.</summary>
    public string? Justificativa { get; private set; }

    /// <summary>
    /// Verdadeiro quando o veterinário alterou o que a IA sugeriu. Separado da decisão
    /// porque é a métrica que diz se a IA está ajudando ou dando trabalho.
    /// </summary>
    public bool AlterouSugestao { get; private set; }

    /// <summary>Modelo que produziu a sugestão, quando houve sugestão.</summary>
    [MaxLength(100)]
    public string? Modelo { get; private set; }

    public DateTime RegistradoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private LogAuditoriaIa() => ConteudoFinal = null!;

    /// <summary>Registra uma decisão. Não há como alterá-la depois.</summary>
    public LogAuditoriaIa(
        Guid consultaId,
        Guid? sessaoCapturaId,
        Guid? rascunhoIaId,
        Guid? veterinarioId,
        DecisaoSobreRascunho decisao,
        string conteudoFinal,
        string? justificativa,
        bool alterouSugestao,
        string? modelo)
    {
        Id = Guid.NewGuid();
        ConsultaId = consultaId;
        SessaoCapturaId = sessaoCapturaId;
        RascunhoIaId = rascunhoIaId;
        VeterinarioId = veterinarioId;
        Decisao = decisao;
        ConteudoFinal = conteudoFinal ?? string.Empty;
        Justificativa = justificativa;
        AlterouSugestao = alterouSugestao;
        Modelo = modelo;
        RegistradoEm = DateTime.UtcNow;
    }
}
