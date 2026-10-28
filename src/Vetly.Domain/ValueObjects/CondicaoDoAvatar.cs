using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Domain.ValueObjects;

/// <summary>
/// Saúde, humor, estado e pelo do avatar do pet, calculados das obrigações e do
/// histórico de cuidado (RN-112/RN-113).
///
/// <para>
/// <b>Tabela de pesos</b> (base 100/100, resultado limitado a 0–100):
/// </para>
/// <list type="table">
/// <listheader><term>Situação</term><description>Saúde / Humor</description></listheader>
/// <item><term>Vacina vencida</term><description>−40 / −10</description></item>
/// <item><term>Vermífugo ou antiparasitário vencido</term><description>−20 / −10</description></item>
/// <item><term>Retorno, medicação contínua ou exame vencido</term><description>−15 / −5</description></item>
/// <item><term>Check-up vencido</term><description>−10 / −5</description></item>
/// <item><term>Higienização vencida</term><description>−5 / −25</description></item>
/// <item><term>Cada 15 dias de atraso, por obrigação (até 3)</term><description>−5 / −5</description></item>
/// <item><term>Sem consulta realizada há mais de 12 meses</term><description>−10 / 0</description></item>
/// <item><term>Peso não registrado há mais de 6 meses</term><description>−5 / 0</description></item>
/// <item><term>Obrigação cumprida no prazo nos últimos 30 dias</term><description>0 / +5 (até +10)</description></item>
/// </list>
/// <para>
/// A assimetria é de propósito: vacina pesa na saúde (é questão sanitária) e pouco no
/// humor; banho atrasado é o contrário — desconforto, não doença. O atraso agrava em
/// degraus de 15 dias, com teto, para que uma obrigação esquecida há um ano não zere
/// sozinha o avatar e esconda as outras.
/// </para>
/// <para>
/// É um cálculo puro, sem relógio próprio: <c>agora</c> vem de fora. Isso é o que
/// permite testar cada linha da tabela e cada limite, e gravar o histórico diário
/// com o mesmo resultado que o app viu.
/// </para>
/// </summary>
/// <param name="Saude">0 a 100.</param>
/// <param name="Humor">0 a 100.</param>
/// <param name="Estado">Estado visual, pela precedência da RN-113.</param>
/// <param name="NivelDoPelo">0 (em dia) a 3 (30 dias ou mais de higiene em atraso).</param>
/// <param name="Motivos">O que derrubou saúde ou humor, do mais grave ao mais leve.</param>
/// <param name="Falas">De uma a três frases do pet, derivadas dos motivos.</param>
public sealed record CondicaoDoAvatar(
    int Saude,
    int Humor,
    EstadoDoAvatar Estado,
    int NivelDoPelo,
    IReadOnlyList<MotivoDoAvatar> Motivos,
    IReadOnlyList<string> Falas)
{
    /// <summary>Saúde e humor de partida, antes de qualquer desconto.</summary>
    public const int Base = 100;

    /// <summary>Abaixo disto, sem vacina nem higiene em atraso, o pet está abatido.</summary>
    public const int LimiteDeAbatimento = 60;

    /// <summary>A partir disto, em saúde e humor, o pet está feliz.</summary>
    public const int LimiteDeFelicidade = 90;

    /// <summary>Tamanho de cada degrau de agravamento por atraso.</summary>
    public const int DiasPorDegrauDeAtraso = 15;

    /// <summary>Máximo de degraus de agravamento por obrigação.</summary>
    public const int DegrausMaximos = 3;

    /// <summary>Teto do bônus de humor por cuidado recente.</summary>
    public const int BonusMaximoDeHumor = 10;

    /// <summary>
    /// Calcula a condição do avatar.
    /// </summary>
    /// <param name="obrigacoes">Obrigações do animal, inclusive arquivadas (que só contam para o bônus).</param>
    /// <param name="ultimaConsultaRealizadaEm">Data da última consulta realizada; nulo se nunca houve.</param>
    /// <param name="pesoRegistradoEm">Quando o peso foi registrado; nulo se nunca foi.</param>
    /// <param name="agora">Instante de referência (UTC).</param>
    public static CondicaoDoAvatar Calcular(
        IEnumerable<ObrigacaoPet> obrigacoes,
        DateTime? ultimaConsultaRealizadaEm,
        DateTime? pesoRegistradoEm,
        DateTime agora)
    {
        var lista = obrigacoes.ToList();

        var saude = Base;
        var humor = Base;
        var motivos = new HashSet<MotivoDoAvatar>();
        var maiorAtrasoDeHigiene = 0;
        var vacinaVencida = false;

        foreach (var obrigacao in lista.Where(o => o.SituacaoEm(agora) == SituacaoObrigacao.Vencida))
        {
            var (perdaDeSaude, perdaDeHumor, motivo) = PesoDe(obrigacao.Tipo);
            var atraso = DiasDeAtraso(obrigacao, agora);
            var degraus = Math.Min(DegrausMaximos, atraso / DiasPorDegrauDeAtraso);

            saude -= perdaDeSaude + degraus * 5;
            humor -= perdaDeHumor + degraus * 5;
            motivos.Add(motivo);

            if (obrigacao.Tipo == TipoObrigacaoPet.Vacina)
                vacinaVencida = true;

            if (obrigacao.Tipo == TipoObrigacaoPet.Higienizacao)
                maiorAtrasoDeHigiene = Math.Max(maiorAtrasoDeHigiene, atraso);
        }

        if (ultimaConsultaRealizadaEm is not { } consulta || consulta < agora.AddMonths(-12))
        {
            saude -= 10;
            motivos.Add(MotivoDoAvatar.SemConsultaHa12Meses);
        }

        if (pesoRegistradoEm is not { } peso || peso < agora.AddMonths(-6))
        {
            saude -= 5;
            motivos.Add(MotivoDoAvatar.PesoDesatualizado);
        }

        // Cuidado em dia rende humor: arquivadas contam, porque um retorno cumprido no
        // prazo se arquiva sozinho e nem por isso deixou de ser cuidado
        var cumpridasNoPrazo = lista.Count(o =>
            o.UltimoCumprimentoNoPrazo
            && o.UltimoCumprimento is { } quando
            && quando <= agora
            && quando >= agora.AddDays(-30));

        humor += Math.Min(BonusMaximoDeHumor, cumpridasNoPrazo * 5);

        saude = Math.Clamp(saude, 0, 100);
        humor = Math.Clamp(humor, 0, 100);

        var higieneVencida = motivos.Contains(MotivoDoAvatar.HigieneVencida);
        var estado = EstadoPor(saude, humor, vacinaVencida, higieneVencida);
        var ordenados = motivos.OrderBy(Gravidade).ToList();

        return new CondicaoDoAvatar(
            saude, humor, estado, NivelDoPeloPara(maiorAtrasoDeHigiene), ordenados, FalasPara(ordenados));
    }

    /// <summary>Perda de saúde e humor de cada tipo de obrigação vencida (RN-112).</summary>
    private static (int Saude, int Humor, MotivoDoAvatar Motivo) PesoDe(TipoObrigacaoPet tipo) => tipo switch
    {
        TipoObrigacaoPet.Vacina => (40, 10, MotivoDoAvatar.VacinaVencida),
        TipoObrigacaoPet.Vermifugo or TipoObrigacaoPet.Antiparasitario => (20, 10, MotivoDoAvatar.VermifugoVencido),
        TipoObrigacaoPet.CheckUp => (10, 5, MotivoDoAvatar.CheckUpVencido),
        TipoObrigacaoPet.Higienizacao => (5, 25, MotivoDoAvatar.HigieneVencida),

        // Retorno, medicação contínua, exame — e qualquer tipo futuro, que entra pelo
        // peso de acompanhamento em vez de ficar sem efeito nenhum
        _ => (15, 5, MotivoDoAvatar.AcompanhamentoVencido)
    };

    /// <summary>
    /// Dias de atraso, arredondados para cima: vencida há uma hora já é um dia de
    /// atraso, e é o que faz o pelo começar a crescer no mesmo dia.
    /// </summary>
    private static int DiasDeAtraso(ObrigacaoPet obrigacao, DateTime agora) =>
        Math.Max(1, (int)Math.Ceiling((agora - obrigacao.ProximoVencimento).TotalDays));

    /// <summary>
    /// Precedência do estado (RN-113): vacina vence tudo (questão sanitária), depois
    /// higiene (o pelo longo é o sinal visual mais forte), depois a saúde baixa, e só
    /// então a felicidade.
    /// </summary>
    private static EstadoDoAvatar EstadoPor(int saude, int humor, bool vacinaVencida, bool higieneVencida)
    {
        if (vacinaVencida)
            return EstadoDoAvatar.VacinaAtrasada;

        if (higieneVencida)
            return EstadoDoAvatar.HigieneAtrasada;

        if (saude < LimiteDeAbatimento)
            return EstadoDoAvatar.Abatido;

        if (humor >= LimiteDeFelicidade && saude >= LimiteDeFelicidade)
            return EstadoDoAvatar.Feliz;

        return EstadoDoAvatar.Saudavel;
    }

    /// <summary>Pelo pelo atraso da higienização: 1–14 dias → 1, 15–29 → 2, 30 ou mais → 3.</summary>
    private static int NivelDoPeloPara(int diasDeAtraso) => diasDeAtraso switch
    {
        <= 0 => 0,
        < 15 => 1,
        < 30 => 2,
        _ => 3
    };

    /// <summary>Ordem de exibição dos motivos: o que mais importa primeiro.</summary>
    private static int Gravidade(MotivoDoAvatar motivo) => motivo switch
    {
        MotivoDoAvatar.VacinaVencida => 1,
        MotivoDoAvatar.VermifugoVencido => 2,
        MotivoDoAvatar.AcompanhamentoVencido => 3,
        MotivoDoAvatar.CheckUpVencido => 4,
        MotivoDoAvatar.HigieneVencida => 5,
        MotivoDoAvatar.SemConsultaHa12Meses => 6,
        _ => 7
    };

    /// <summary>
    /// Até três falas, uma por motivo, na ordem de gravidade. Textos fixos: o app
    /// mostra o que a API manda, e mudar uma frase não exige publicar o app.
    ///
    /// Sem motivo, a condição é sempre 100/100 — todo desconto da tabela vem com um
    /// motivo —, e por isso a única fala possível é a do pet feliz.
    /// </summary>
    private static IReadOnlyList<string> FalasPara(IReadOnlyList<MotivoDoAvatar> motivos) =>
        motivos.Count == 0
            ? ["Estou ótimo! Obrigado por cuidar de mim."]
            : motivos.Take(3).Select(FalaDe).ToList();

    private static string FalaDe(MotivoDoAvatar motivo) => motivo switch
    {
        MotivoDoAvatar.VacinaVencida => "Minha vacina venceu. Me leva para tomar?",
        MotivoDoAvatar.VermifugoVencido => "Está na hora do meu vermífugo.",
        MotivoDoAvatar.AcompanhamentoVencido => "Tenho um acompanhamento pendente com o veterinário.",
        MotivoDoAvatar.CheckUpVencido => "Faz tempo que não faço meu check-up.",
        MotivoDoAvatar.HigieneVencida => "Estou precisando de um banho!",
        MotivoDoAvatar.SemConsultaHa12Meses => "Faz mais de um ano que não vou ao veterinário.",
        _ => "Que tal me pesar de novo?"
    };
}
