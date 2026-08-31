using Vetly.Application.DTOs.Captura;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Documento;

/// <summary>
/// Tudo que uma factory precisa para formatar um documento (RN-083).
///
/// O ponto do contrato é o <see cref="Conteudo"/>: ele vem do estado final aprovado
/// pelo veterinário, não da IA nem de nova inferência. Gerar documento é
/// <b>formatar</b> o que já foi decidido — se a factory precisasse consultar a IA
/// outra vez, o que fosse impresso poderia divergir do que o profissional aprovou.
/// </summary>
public class ContextoDoDocumentoDto
{
    public Guid ConsultaId { get; set; }
    public DateTime DataDoAtendimento { get; set; }
    public ModalidadeAtendimento Modalidade { get; set; }

    public string VeterinarioNome { get; set; } = string.Empty;
    public string Crmv { get; set; } = string.Empty;
    public string UfAtuacao { get; set; } = string.Empty;

    public string AnimalNome { get; set; } = string.Empty;
    public string Especie { get; set; } = string.Empty;
    public string Raca { get; set; } = string.Empty;
    public DateTime? DataNascimento { get; set; }

    /// <summary>Nulo quando o peso não está cadastrado (RN-081).</summary>
    public decimal? PesoKg { get; set; }

    public string? Sexo { get; set; }

    public string TutorNome { get; set; } = string.Empty;

    /// <summary>Conteúdo clínico como o veterinário o aprovou (RN-082/RN-083).</summary>
    public ConteudoDoProntuarioDto Conteudo { get; set; } = new();

    /// <summary>Subtipo, quando o documento é um atestado (RN-086).</summary>
    public TipoAtestado? SubtipoAtestado { get; set; }

    /// <summary>Valor do atendimento, usado na nota fiscal.</summary>
    public decimal? ValorDoAtendimento { get; set; }

    /// <summary>Idade em anos completos na data do atendimento, quando há nascimento.</summary>
    public int? IdadeAnos => DataNascimento is { } nascimento
        ? Math.Max(0, (int)((DataDoAtendimento - nascimento).TotalDays / 365.25))
        : null;
}
