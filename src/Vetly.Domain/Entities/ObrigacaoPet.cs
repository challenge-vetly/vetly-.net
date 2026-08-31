using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Uma obrigação recorrente de cuidado do animal — vacina, vermífugo, antipulgas,
/// retorno (RN-045/RN-046).
///
/// É o que transforma "o pet está bem" numa afirmação verificável. O Responsável não
/// tem como lembrar sozinho de seis reforços com periodicidades diferentes, e o
/// veterinário só descobre o atraso quando o animal já voltou doente.
///
/// A obrigação guarda a <b>periodicidade</b>, não uma data solta: cumprir empurra o
/// próximo vencimento sozinho. Sem isso, cada cumprimento exigiria alguém lembrar de
/// reagendar o seguinte, que é exatamente o que falha.
/// </summary>
public class ObrigacaoPet
{
    /// <summary>Janela em que a obrigação já aparece como "vencendo" no board.</summary>
    public static readonly TimeSpan JanelaDeAviso = TimeSpan.FromDays(30);

    /// <summary>Identificador da obrigação (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Animal a que a obrigação pertence.</summary>
    [Required]
    public Guid AnimalId { get; private set; }

    /// <summary>Responsável pelo animal — é quem recebe o aviso.</summary>
    [Required]
    public Guid TutorId { get; private set; }

    /// <summary>Natureza da obrigação.</summary>
    [Required]
    public TipoObrigacaoPet Tipo { get; private set; }

    /// <summary>
    /// Descrição específica: "V10", "Antirrábica", "Vermífugo de amplo espectro".
    /// O tipo diz a categoria; isto diz o que de fato precisa ser feito.
    /// </summary>
    [Required]
    [MaxLength(120)]
    public string Descricao { get; private set; }

    /// <summary>
    /// De quantos em quantos dias se repete. Zero para obrigação de uma vez só —
    /// um retorno pontual, por exemplo.
    /// </summary>
    public int PeriodicidadeEmDias { get; private set; }

    /// <summary>Quando vence a próxima vez.</summary>
    public DateTime ProximoVencimento { get; private set; }

    /// <summary>Última vez que foi cumprida. Nulo enquanto nunca foi.</summary>
    public DateTime? UltimoCumprimento { get; private set; }

    /// <summary>Consulta em que foi cumprida pela última vez, quando houve uma.</summary>
    public Guid? UltimaConsultaId { get; private set; }

    /// <summary>Veterinário que registrou o último cumprimento.</summary>
    public Guid? RegistradaPorVeterinarioId { get; private set; }

    /// <summary>
    /// Obrigação criada a partir da carteira de vacinação já existente, e não digitada
    /// por alguém. Fica marcado porque muda o quanto se pode confiar na data.
    /// </summary>
    public bool DerivadaDaCarteira { get; private set; }

    /// <summary>
    /// Obrigação arquivada deixa de aparecer no board sem sumir do histórico — o animal
    /// mudou de protocolo, ou a vacina não se aplica mais à idade dele.
    /// </summary>
    public bool Arquivada { get; private set; }

    public DateTime CriadaEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private ObrigacaoPet() => Descricao = null!;

    /// <summary>Cria uma obrigação de cuidado para o animal (RN-045).</summary>
    public ObrigacaoPet(
        Guid animalId,
        Guid tutorId,
        TipoObrigacaoPet tipo,
        string descricao,
        DateTime proximoVencimento,
        int periodicidadeEmDias = 0,
        bool derivadaDaCarteira = false)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("A descrição da obrigação é obrigatória.", nameof(descricao));

        if (periodicidadeEmDias < 0)
            throw new ArgumentOutOfRangeException(nameof(periodicidadeEmDias),
                "A periodicidade não pode ser negativa.");

        Id = Guid.NewGuid();
        AnimalId = animalId;
        TutorId = tutorId;
        Tipo = tipo;
        Descricao = descricao.Trim();
        ProximoVencimento = proximoVencimento;
        PeriodicidadeEmDias = periodicidadeEmDias;
        DerivadaDaCarteira = derivadaDaCarteira;
        CriadaEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Registra o cumprimento e empurra o próximo vencimento (RN-045).
    ///
    /// O próximo vencimento conta a partir do <b>cumprimento</b>, não do vencimento
    /// anterior: quem vacinou com dois meses de atraso não deve receber o próximo
    /// aviso dois meses adiantado.
    /// </summary>
    public void Cumprir(DateTime quando, Guid? consultaId = null, Guid? veterinarioId = null)
    {
        UltimoCumprimento = quando;
        UltimaConsultaId = consultaId;
        RegistradaPorVeterinarioId = veterinarioId;

        if (PeriodicidadeEmDias > 0)
        {
            ProximoVencimento = quando.AddDays(PeriodicidadeEmDias);
            return;
        }

        // Obrigação de uma vez só se encerra ao ser cumprida, em vez de ficar
        // eternamente vencida no board
        Arquivada = true;
    }

    /// <summary>Reagenda o vencimento — o veterinário ajustou o protocolo.</summary>
    public void Reagendar(DateTime novoVencimento) => ProximoVencimento = novoVencimento;

    /// <summary>Tira do board sem apagar do histórico.</summary>
    public void Arquivar() => Arquivada = true;

    /// <summary>Traz de volta ao board.</summary>
    public void Reativar() => Arquivada = false;

    /// <summary>Situação da obrigação em relação a um instante (RN-045).</summary>
    public SituacaoObrigacao SituacaoEm(DateTime agora)
    {
        if (Arquivada)
            return SituacaoObrigacao.Arquivada;

        if (ProximoVencimento <= agora)
            return SituacaoObrigacao.Vencida;

        return ProximoVencimento - agora <= JanelaDeAviso
            ? SituacaoObrigacao.Vencendo
            : SituacaoObrigacao.EmDia;
    }

    /// <summary>Dias até vencer; negativo quando já venceu.</summary>
    public int DiasAteVencer(DateTime agora) => (int)Math.Ceiling((ProximoVencimento - agora).TotalDays);
}
