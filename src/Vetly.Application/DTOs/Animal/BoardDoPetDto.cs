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
    /// Avatar completo — saúde, humor, estado, pelo, motivos e falas (RN-112 a
    /// RN-114). O mesmo de <c>GET /api/animais/{id}/avatar</c>.
    ///
    /// Até a Sprint 3 a API produzia só o estado, e a documentação chamava a rota
    /// <c>/avatar</c> de regressão de escopo. A decisão foi revertida porque o app
    /// passou a simular saúde, humor e pelo com valores fixos: a regra precisava sair
    /// do front. Sprite e animação continuam assets do app.
    /// </summary>
    public AvatarDoPetDto Avatar { get; set; } = new();

    /// <summary>
    /// <b>Mantido por compatibilidade</b> — use <see cref="Avatar"/>. Só os três
    /// valores originais (<c>Saudavel</c>, <c>VacinaAtrasada</c>,
    /// <c>HigieneAtrasada</c>): <c>Abatido</c> e <c>Feliz</c> aparecem aqui como
    /// <c>Saudavel</c>, para o app publicado não receber um texto que não conhece.
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
