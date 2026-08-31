using Vetly.Application.DTOs.Documento;
using Vetly.Application.DTOs.Obrigacao;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Animal;

/// <summary>
/// O board do pet: a tela inicial do Responsável dentro do app
/// (RN-011/RN-020/RN-090/RN-096).
///
/// Junta o que está pendente, o que vem depois e o que chegou. São três perguntas
/// que o Responsável faz ao abrir o app — "falta alguma coisa?", "quando é a próxima
/// consulta?", "saiu algum documento?" — e uma tela que respondesse só uma delas
/// obrigaria a navegar para descobrir o resto.
/// </summary>
public class BoardDoPetDto
{
    public Guid AnimalId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Especie { get; set; } = string.Empty;
    public string Raca { get; set; } = string.Empty;
    public int? IdadeAnos { get; set; }
    public decimal? PesoKg { get; set; }
    public Guid? FotoMidiaId { get; set; }

    /// <summary>
    /// Estado visual do avatar, derivado das obrigações (RN-020/RN-096).
    ///
    /// É o <b>único</b> dado do avatar que a API produz: o sprite, a animação e a
    /// "reclamação" são assets no bundle do app (C3). Não há `TB_AVATAR` nem rota
    /// `/avatar` — construir uma seria regressão de escopo.
    /// </summary>
    public EstadoDoAvatar AvatarEstado { get; set; }

    /// <summary>Obrigações ativas, das mais urgentes às menos (RN-045/RN-046).</summary>
    public List<ObrigacaoPetDto> Obrigacoes { get; set; } = [];

    /// <summary>Verdadeiro quando há obrigação vencida.</summary>
    public bool TemPendencia { get; set; }

    /// <summary>Próximos atendimentos agendados.</summary>
    public List<AgendamentoDoBoardDto> ProximosAgendamentos { get; set; } = [];

    /// <summary>Documentos publicados recentemente (RN-011/RN-090).</summary>
    public List<DocumentoDto> DocumentosRecentes { get; set; } = [];

    /// <summary>Alertas clínicos que nunca podem ser ocultados (RN-068).</summary>
    public List<string> AlertasDeSeguranca { get; set; } = [];
}

/// <summary>Um agendamento futuro, na visão do board.</summary>
public class AgendamentoDoBoardDto
{
    public Guid ConsultaId { get; set; }
    public DateTime DataHora { get; set; }
    public Guid VeterinarioId { get; set; }
    public string VeterinarioNome { get; set; } = string.Empty;
    public StatusConsulta Status { get; set; }
    public ModalidadeAtendimento Modalidade { get; set; }
}
