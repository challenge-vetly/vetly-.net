using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Cálculo do avatar (RN-112/RN-113): cada linha da tabela de pesos, os limites de
/// 0 e 100, os degraus de atraso, a precedência de estado e o pelo.
///
/// Todos os cenários partem de um pet "em ordem" — consulta recente e peso recente —
/// para que só a linha sob teste mexa nos números.
/// </summary>
public class CondicaoDoAvatarTests
{
    private static readonly DateTime Agora = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ConsultaRecente = Agora.AddMonths(-2);
    private static readonly DateTime PesoRecente = Agora.AddMonths(-1);

    private static readonly Guid Animal = Guid.NewGuid();
    private static readonly Guid Tutor = Guid.NewGuid();

    private static ObrigacaoPet Vencida(TipoObrigacaoPet tipo, int diasDeAtraso) =>
        new(Animal, Tutor, tipo, tipo.ToString(), Agora.AddDays(-diasDeAtraso), 30);

    private static ObrigacaoPet EmDia(TipoObrigacaoPet tipo) =>
        new(Animal, Tutor, tipo, tipo.ToString(), Agora.AddDays(60), 30);

    private static CondicaoDoAvatar Calcular(params ObrigacaoPet[] obrigacoes) =>
        CondicaoDoAvatar.Calcular(obrigacoes, ConsultaRecente, PesoRecente, Agora);

    // ── Base ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_SemNadaVencido_ParteDaBase100EFicaFeliz()
    {
        // Arrange
        var obrigacoes = new[] { EmDia(TipoObrigacaoPet.Vacina), EmDia(TipoObrigacaoPet.Higienizacao) };

        // Act
        var condicao = Calcular(obrigacoes);

        // Assert
        Assert.Equal(100, condicao.Saude);
        Assert.Equal(100, condicao.Humor);
        Assert.Equal(EstadoDoAvatar.Feliz, condicao.Estado);
        Assert.Equal(0, condicao.NivelDoPelo);
        Assert.Empty(condicao.Motivos);
        Assert.Equal(["Estou ótimo! Obrigado por cuidar de mim."], condicao.Falas);
    }

    // ── Uma linha da tabela por tipo vencido (1 a 14 dias: sem degrau) ───────

    [Theory]
    [InlineData(TipoObrigacaoPet.Vacina, 60, 90, MotivoDoAvatar.VacinaVencida)]
    [InlineData(TipoObrigacaoPet.Vermifugo, 80, 90, MotivoDoAvatar.VermifugoVencido)]
    [InlineData(TipoObrigacaoPet.Antiparasitario, 80, 90, MotivoDoAvatar.VermifugoVencido)]
    [InlineData(TipoObrigacaoPet.Retorno, 85, 95, MotivoDoAvatar.AcompanhamentoVencido)]
    [InlineData(TipoObrigacaoPet.MedicacaoContinua, 85, 95, MotivoDoAvatar.AcompanhamentoVencido)]
    [InlineData(TipoObrigacaoPet.Exame, 85, 95, MotivoDoAvatar.AcompanhamentoVencido)]
    [InlineData(TipoObrigacaoPet.CheckUp, 90, 95, MotivoDoAvatar.CheckUpVencido)]
    [InlineData(TipoObrigacaoPet.Higienizacao, 95, 75, MotivoDoAvatar.HigieneVencida)]
    public void Calcular_ObrigacaoVencida_AplicaOPesoDaTabela(
        TipoObrigacaoPet tipo, int saudeEsperada, int humorEsperado, MotivoDoAvatar motivo)
    {
        // Arrange
        var vencida = Vencida(tipo, diasDeAtraso: 5);

        // Act
        var condicao = Calcular(vencida);

        // Assert
        Assert.Equal(saudeEsperada, condicao.Saude);
        Assert.Equal(humorEsperado, condicao.Humor);
        Assert.Equal([motivo], condicao.Motivos);
    }

    // ── Degraus de atraso: −5/−5 a cada 15 dias, até 3 ───────────────────────

    [Theory]
    [InlineData(1, 80, 90)]
    [InlineData(14, 80, 90)]
    [InlineData(15, 75, 85)]
    [InlineData(29, 75, 85)]
    [InlineData(30, 70, 80)]
    [InlineData(45, 65, 75)]
    [InlineData(400, 65, 75)]
    public void Calcular_AtrasoAgravaEmDegrausDe15DiasComTeto(int diasDeAtraso, int saude, int humor)
    {
        // Arrange — vermifugo: base 80/90 sem degrau
        var vencida = Vencida(TipoObrigacaoPet.Vermifugo, diasDeAtraso);

        // Act
        var condicao = Calcular(vencida);

        // Assert
        Assert.Equal(saude, condicao.Saude);
        Assert.Equal(humor, condicao.Humor);
    }

    [Fact]
    public void Calcular_VencidaHaMenosDeUmDia_JaContaComoUmDiaDeAtraso()
    {
        // Arrange — venceu ha uma hora
        var obrigacao = new ObrigacaoPet(Animal, Tutor, TipoObrigacaoPet.Higienizacao, "Banho", Agora.AddHours(-1), 30);

        // Act
        var condicao = Calcular(obrigacao);

        // Assert — o pelo ja comeca a crescer
        Assert.Equal(1, condicao.NivelDoPelo);
    }

    [Fact]
    public void Calcular_DegrausSaoPorObrigacao()
    {
        // Arrange — duas vencidas ha 30 dias: cada uma leva os proprios 2 degraus
        var vermifugo = Vencida(TipoObrigacaoPet.Vermifugo, 30);
        var checkUp = Vencida(TipoObrigacaoPet.CheckUp, 30);

        // Act
        var condicao = Calcular(vermifugo, checkUp);

        // Assert — 100 − (20+10) − (10+10) = 50; 100 − (10+10) − (5+10) = 65
        Assert.Equal(50, condicao.Saude);
        Assert.Equal(65, condicao.Humor);
    }

    // ── Consulta e peso ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(-11, false)]
    [InlineData(-13, true)]
    public void Calcular_SemConsultaHa12Meses_Perde10DeSaude(int mesesDesdeAConsulta, bool penaliza)
    {
        // Arrange
        var consulta = Agora.AddMonths(mesesDesdeAConsulta);

        // Act
        var condicao = CondicaoDoAvatar.Calcular([], consulta, PesoRecente, Agora);

        // Assert
        Assert.Equal(penaliza ? 90 : 100, condicao.Saude);
        Assert.Equal(100, condicao.Humor);
        Assert.Equal(penaliza, condicao.Motivos.Contains(MotivoDoAvatar.SemConsultaHa12Meses));
    }

    [Fact]
    public void Calcular_NuncaTeveConsulta_ContaComoSemConsultaHa12Meses()
    {
        // Arrange / Act
        var condicao = CondicaoDoAvatar.Calcular([], null, PesoRecente, Agora);

        // Assert
        Assert.Equal(90, condicao.Saude);
        Assert.Contains(MotivoDoAvatar.SemConsultaHa12Meses, condicao.Motivos);
    }

    [Theory]
    [InlineData(-5, false)]
    [InlineData(-7, true)]
    public void Calcular_PesoNaoRegistradoHa6Meses_Perde5DeSaude(int mesesDesdeOPeso, bool penaliza)
    {
        // Arrange
        var peso = Agora.AddMonths(mesesDesdeOPeso);

        // Act
        var condicao = CondicaoDoAvatar.Calcular([], ConsultaRecente, peso, Agora);

        // Assert
        Assert.Equal(penaliza ? 95 : 100, condicao.Saude);
        Assert.Equal(penaliza, condicao.Motivos.Contains(MotivoDoAvatar.PesoDesatualizado));
    }

    [Fact]
    public void Calcular_PesoNuncaRegistrado_ContaComoDesatualizado()
    {
        // Arrange / Act
        var condicao = CondicaoDoAvatar.Calcular([], ConsultaRecente, null, Agora);

        // Assert
        Assert.Equal(95, condicao.Saude);
        Assert.Contains(MotivoDoAvatar.PesoDesatualizado, condicao.Motivos);
    }

    // ── Bônus de cuidado recente: +5 de humor por cumprida no prazo, até +10 ─

    private static ObrigacaoPet CumpridaNoPrazo(int diasAtras)
    {
        var obrigacao = new ObrigacaoPet(Animal, Tutor, TipoObrigacaoPet.Vermifugo, "V", Agora.AddDays(10), 90);
        obrigacao.Cumprir(Agora.AddDays(-diasAtras));
        return obrigacao;
    }

    [Theory]
    [InlineData(1, 80)]
    [InlineData(2, 85)]
    [InlineData(3, 85)]
    public void Calcular_CumpridasNoPrazoNosUltimos30Dias_SomamHumorAteODobro(int quantidade, int humorEsperado)
    {
        // Arrange — higiene vencida ha 5 dias derruba o humor para 75
        var obrigacoes = Enumerable.Range(0, quantidade).Select(_ => CumpridaNoPrazo(3))
            .Append(Vencida(TipoObrigacaoPet.Higienizacao, 5))
            .ToArray();

        // Act
        var condicao = Calcular(obrigacoes);

        // Assert
        Assert.Equal(humorEsperado, condicao.Humor);
    }

    [Fact]
    public void Calcular_CumpridaHaMaisDe30Dias_NaoDaBonus()
    {
        // Arrange
        var antiga = CumpridaNoPrazo(31);
        var higiene = Vencida(TipoObrigacaoPet.Higienizacao, 5);

        // Act
        var condicao = Calcular(antiga, higiene);

        // Assert
        Assert.Equal(75, condicao.Humor);
    }

    [Fact]
    public void Calcular_CumpridaComAtraso_NaoDaBonus()
    {
        // Arrange — cumprida depois do vencimento
        var atrasada = new ObrigacaoPet(Animal, Tutor, TipoObrigacaoPet.Vermifugo, "V", Agora.AddDays(-10), 90);
        atrasada.Cumprir(Agora.AddDays(-2));
        var higiene = Vencida(TipoObrigacaoPet.Higienizacao, 5);

        // Act
        var condicao = Calcular(atrasada, higiene);

        // Assert
        Assert.Equal(75, condicao.Humor);
    }

    [Fact]
    public void Calcular_ObrigacaoArquivadaCumpridaNoPrazo_ContaParaOBonusMasNaoComoVencida()
    {
        // Arrange — retorno de uma vez so: cumprir arquiva
        var retorno = new ObrigacaoPet(Animal, Tutor, TipoObrigacaoPet.Retorno, "Retorno", Agora.AddDays(2));
        retorno.Cumprir(Agora.AddDays(-1));
        var higiene = Vencida(TipoObrigacaoPet.Higienizacao, 5);

        // Act
        var condicao = Calcular(retorno, higiene);

        // Assert
        Assert.True(retorno.Arquivada);
        Assert.Equal(80, condicao.Humor);
        Assert.DoesNotContain(MotivoDoAvatar.AcompanhamentoVencido, condicao.Motivos);
    }

    [Fact]
    public void Calcular_ObrigacaoArquivadaVencida_NaoPenaliza()
    {
        // Arrange
        var arquivada = Vencida(TipoObrigacaoPet.Vacina, 50);
        arquivada.Arquivar();

        // Act
        var condicao = Calcular(arquivada);

        // Assert
        Assert.Equal(100, condicao.Saude);
    }

    // ── Limites ──────────────────────────────────────────────────────────────

    [Fact]
    public void Calcular_DescontosAcimaDe100_FicamEmZero()
    {
        // Arrange — tres vacinas atrasadas ha 60 dias: −55 cada, mais consulta e peso
        var obrigacoes = Enumerable.Range(0, 3).Select(_ => Vencida(TipoObrigacaoPet.Vacina, 60)).ToArray();

        // Act
        var condicao = CondicaoDoAvatar.Calcular(obrigacoes, null, null, Agora);

        // Assert
        Assert.Equal(0, condicao.Saude);
        Assert.InRange(condicao.Humor, 0, 100);
    }

    [Fact]
    public void Calcular_BonusNaoPassaDe100()
    {
        // Arrange — tudo em dia e duas cumpridas no prazo
        var obrigacoes = new[] { CumpridaNoPrazo(1), CumpridaNoPrazo(2) };

        // Act
        var condicao = Calcular(obrigacoes);

        // Assert
        Assert.Equal(100, condicao.Humor);
    }

    // ── Precedência do estado (RN-113) ───────────────────────────────────────

    [Fact]
    public void Estado_VacinaTemPrecedenciaSobreHigiene()
    {
        // Arrange
        var obrigacoes = new[] { Vencida(TipoObrigacaoPet.Higienizacao, 40), Vencida(TipoObrigacaoPet.Vacina, 1) };

        // Act
        var condicao = Calcular(obrigacoes);

        // Assert
        Assert.Equal(EstadoDoAvatar.VacinaAtrasada, condicao.Estado);
    }

    [Fact]
    public void Estado_HigieneTemPrecedenciaSobreAbatido()
    {
        // Arrange — saude abaixo de 60 por outros motivos, mas higiene vencida
        var obrigacoes = new[]
        {
            Vencida(TipoObrigacaoPet.Higienizacao, 2),
            Vencida(TipoObrigacaoPet.Vermifugo, 45),
            Vencida(TipoObrigacaoPet.Retorno, 45)
        };

        // Act
        var condicao = Calcular(obrigacoes);

        // Assert
        Assert.True(condicao.Saude < CondicaoDoAvatar.LimiteDeAbatimento);
        Assert.Equal(EstadoDoAvatar.HigieneAtrasada, condicao.Estado);
    }

    [Fact]
    public void Estado_SaudeExatamente60_AindaNaoEAbatido()
    {
        // Arrange — vermifugo com 15 dias (−25/−15) e retorno (−15/−5): saude 60
        var obrigacoes = new[] { Vencida(TipoObrigacaoPet.Vermifugo, 15), Vencida(TipoObrigacaoPet.Retorno, 1) };

        // Act
        var condicao = Calcular(obrigacoes);

        // Assert
        Assert.Equal(60, condicao.Saude);
        Assert.Equal(EstadoDoAvatar.Saudavel, condicao.Estado);
    }

    [Fact]
    public void Estado_SaudeAbaixoDe60SemVacinaNemHigiene_EAbatido()
    {
        // Arrange — o mesmo cenario com o peso desatualizado (−5): saude 55
        var obrigacoes = new[] { Vencida(TipoObrigacaoPet.Vermifugo, 15), Vencida(TipoObrigacaoPet.Retorno, 1) };

        // Act
        var condicao = CondicaoDoAvatar.Calcular(obrigacoes, ConsultaRecente, Agora.AddMonths(-7), Agora);

        // Assert
        Assert.Equal(55, condicao.Saude);
        Assert.Equal(EstadoDoAvatar.Abatido, condicao.Estado);
    }

    [Fact]
    public void Estado_SaudeExatamente90_AindaEFeliz()
    {
        // Arrange — check-up vencido: 90/95
        var checkUp = Vencida(TipoObrigacaoPet.CheckUp, 1);

        // Act
        var condicao = Calcular(checkUp);

        // Assert
        Assert.Equal(90, condicao.Saude);
        Assert.Equal(95, condicao.Humor);
        Assert.Equal(EstadoDoAvatar.Feliz, condicao.Estado);
    }

    [Fact]
    public void Estado_SaudeAbaixoDe90_DeixaDeSerFeliz()
    {
        // Arrange — retorno vencido: 85/95. Fora da higiene, toda linha tira de saude
        // pelo menos o que tira de humor; por isso e a saude que decide entre Feliz e
        // Saudavel quando nao ha higiene vencida
        var retorno = Vencida(TipoObrigacaoPet.Retorno, 1);

        // Act
        var condicao = Calcular(retorno);

        // Assert
        Assert.Equal(85, condicao.Saude);
        Assert.Equal(EstadoDoAvatar.Saudavel, condicao.Estado);
    }

    // ── Pelo ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, 1)]
    [InlineData(14, 1)]
    [InlineData(15, 2)]
    [InlineData(29, 2)]
    [InlineData(30, 3)]
    [InlineData(200, 3)]
    public void NivelDoPelo_SegueOAtrasoDaHigienizacao(int diasDeAtraso, int nivel)
    {
        // Arrange
        var higiene = Vencida(TipoObrigacaoPet.Higienizacao, diasDeAtraso);

        // Act
        var condicao = Calcular(higiene);

        // Assert
        Assert.Equal(nivel, condicao.NivelDoPelo);
    }

    [Fact]
    public void NivelDoPelo_OutraObrigacaoVencida_NaoMexeNoPelo()
    {
        // Arrange — o atalho antigo deixava o pet "de pelo longo" por antipulgas
        var antipulgas = Vencida(TipoObrigacaoPet.Antiparasitario, 40);

        // Act
        var condicao = Calcular(antipulgas);

        // Assert
        Assert.Equal(0, condicao.NivelDoPelo);
        Assert.NotEqual(EstadoDoAvatar.HigieneAtrasada, condicao.Estado);
    }

    [Fact]
    public void NivelDoPelo_DuasHigienizacoes_ValeOMaiorAtraso()
    {
        // Arrange
        var banho = Vencida(TipoObrigacaoPet.Higienizacao, 3);
        var tosa = Vencida(TipoObrigacaoPet.Higienizacao, 31);

        // Act
        var condicao = Calcular(banho, tosa);

        // Assert
        Assert.Equal(3, condicao.NivelDoPelo);
    }

    // ── Motivos e falas ──────────────────────────────────────────────────────

    [Fact]
    public void Motivos_VemDoMaisGraveAoMaisLeve_EFalasLimitadasATres()
    {
        // Arrange
        var obrigacoes = new[]
        {
            Vencida(TipoObrigacaoPet.Higienizacao, 2),
            Vencida(TipoObrigacaoPet.CheckUp, 2),
            Vencida(TipoObrigacaoPet.Vacina, 2),
            Vencida(TipoObrigacaoPet.Vermifugo, 2)
        };

        // Act
        var condicao = CondicaoDoAvatar.Calcular(obrigacoes, null, null, Agora);

        // Assert
        Assert.Equal(
            [
                MotivoDoAvatar.VacinaVencida, MotivoDoAvatar.VermifugoVencido, MotivoDoAvatar.CheckUpVencido,
                MotivoDoAvatar.HigieneVencida, MotivoDoAvatar.SemConsultaHa12Meses, MotivoDoAvatar.PesoDesatualizado
            ],
            condicao.Motivos);
        Assert.Equal(3, condicao.Falas.Count);
        Assert.Equal("Minha vacina venceu. Me leva para tomar?", condicao.Falas[0]);
    }

    [Fact]
    public void Motivos_DuasVacinasVencidas_AparecemUmaVez()
    {
        // Arrange
        var obrigacoes = new[] { Vencida(TipoObrigacaoPet.Vacina, 2), Vencida(TipoObrigacaoPet.Vacina, 3) };

        // Act
        var condicao = Calcular(obrigacoes);

        // Assert — mas as duas descontam: 100 − 40 − 40
        Assert.Equal([MotivoDoAvatar.VacinaVencida], condicao.Motivos);
        Assert.Equal(20, condicao.Saude);
    }
}
