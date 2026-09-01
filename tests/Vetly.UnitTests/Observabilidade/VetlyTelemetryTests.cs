using System.Diagnostics;
using Vetly.Application.Observability;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitários do contrato de telemetria da camada de Aplicação
/// (<see cref="VetlyTelemetry"/>): nomes, tags e comportamento dos spans.
/// </summary>
/// <remarks>
/// <para>
/// O que se testa aqui é <b>contrato</b>, não implementação. O nome de uma métrica e o
/// nome de uma tag são a interface pública consumida por painéis, alertas e consultas
/// no Prometheus — renomear <c>vetly.regras.violadas</c> quebra o alerta de plantão tão
/// literalmente quanto renomear uma rota quebra o app. Compilar continua funcionando;
/// o alerta é que para de disparar. Estes testes transformam esse contrato invisível em
/// algo que falha no CI.
/// </para>
/// <para>Todos seguem o padrão <b>AAA</b>.</para>
/// </remarks>
[Collection(ColecaoDeTelemetria.Nome)]
public class VetlyTelemetryTests
{
    private readonly ColetorDeTelemetriaFixture _coletor;

    public VetlyTelemetryTests(ColetorDeTelemetriaFixture coletor) => _coletor = coletor;

    // ── Identidade do serviço ────────────────────────────────────────────────

    [Fact]
    public void Identidade_DoServico_EstaFixadaNoContrato()
    {
        // Arrange — o nome do serviço vai para o Resource do OpenTelemetry, e é por ele
        // que a Vetly é encontrada entre os demais serviços do mesmo backend.

        // Act
        var nome = VetlyTelemetry.NomeDoServico;

        // Assert
        Assert.Equal("vetly-api", nome);
        Assert.Equal("Vetly.Application", VetlyTelemetry.NomeDaFonte);
        Assert.Equal("Vetly.Negocio", VetlyTelemetry.NomeDoMedidor);
    }

    // ── Métricas ─────────────────────────────────────────────────────────────

    [Fact]
    public void RegrasVioladas_RegistraOCodigoComoTag()
    {
        // Arrange — sentinela próprio: a suíte roda em paralelo e outras classes também
        // emitem neste contador, então o teste precisa de um valor que só ele produz.
        const string codigoSentinela = "RN-TESTE-CONTRATO";
        _coletor.Limpar();

        // Act
        VetlyTelemetry.RegrasVioladas.Add(1,
            new KeyValuePair<string, object?>("codigo", codigoSentinela));

        // Assert
        var medicao = _coletor.De("vetly.regras.violadas")
            .SingleOrDefault(m => m.Tag("codigo") == codigoSentinela);

        Assert.NotNull(medicao);
        Assert.Equal(1, medicao.Valor);
    }

    [Fact]
    public void CheckoutsIniciados_SeparaClinicaDeAutonomo()
    {
        // Arrange — RN-003: com clínica, a consulta é atribuída ao profissional que ela
        // designa; com autônomo, direto com ele. São funis diferentes e precisam ser
        // legíveis separadamente.
        _coletor.Limpar();

        // Act
        VetlyTelemetry.CheckoutsIniciados.Add(1, new KeyValuePair<string, object?>("prestador", "clinica"));
        VetlyTelemetry.CheckoutsIniciados.Add(1, new KeyValuePair<string, object?>("prestador", "autonomo"));

        // Assert
        var medicoes = _coletor.De("vetly.checkouts.iniciados");

        Assert.Contains(medicoes, m => m.Tag("prestador") == "clinica");
        Assert.Contains(medicoes, m => m.Tag("prestador") == "autonomo");
    }

    [Fact]
    public void ValorTransacionado_ERegistradoComoHistograma()
    {
        // Arrange — histograma e não contador: a distribuição do ticket importa mais
        // que a soma. Uma clínica com ticket médio de R$ 200 pode estar vendendo
        // consultas de R$ 80 e cirurgias de R$ 900, e o split (RN-070) incide sobre
        // cada transação, não sobre a média.
        _coletor.Limpar();

        // Act
        VetlyTelemetry.ValorTransacionado.Record(200.00);
        VetlyTelemetry.ValorTransacionado.Record(80.50);

        // Assert
        var valores = _coletor.De("vetly.pagamentos.valor").Select(m => m.Valor).ToList();

        Assert.Contains(200.00, valores);
        Assert.Contains(80.50, valores);
    }

    [Fact]
    public void DecisoesSobreRascunho_CobremOsTresDesfechosDaRN082()
    {
        // Arrange — a métrica-alvo do MVP (§10) é a proporção de documentos gerados pela
        // IA sem edição relevante. Ela só existe se os três desfechos forem contados no
        // MESMO instrumento: contar só os aprovados daria um numerador sem denominador.
        _coletor.Limpar();

        // Act
        foreach (var decisao in new[] { "Aprovado", "Corrigido", "NaoAprovado" })
            VetlyTelemetry.DecisoesSobreRascunho.Add(1,
                new KeyValuePair<string, object?>("decisao", decisao));

        // Assert
        var decisoes = _coletor.De("vetly.ia.decisoes").Select(m => m.Tag("decisao")).ToList();

        Assert.Contains("Aprovado", decisoes);
        Assert.Contains("Corrigido", decisoes);
        Assert.Contains("NaoAprovado", decisoes);
    }

    [Fact]
    public void JobsExecutados_DistinguemSucessoDeFalha()
    {
        // Arrange — o worker roda fora da requisição (§11). Uma fila que parou de drenar
        // não gera erro para ninguém: aparece como lembrete que nunca chegou, dias depois.
        _coletor.Limpar();

        // Act
        VetlyTelemetry.JobsExecutados.Add(1,
            new KeyValuePair<string, object?>("tipo", "PromoverListaEspera"),
            new KeyValuePair<string, object?>("resultado", "sucesso"));

        VetlyTelemetry.JobsExecutados.Add(1,
            new KeyValuePair<string, object?>("tipo", "PromoverListaEspera"),
            new KeyValuePair<string, object?>("resultado", "falha"));

        // Assert
        var medicoes = _coletor.De("vetly.jobs.executados")
            .Where(m => m.Tag("tipo") == "PromoverListaEspera")
            .ToList();

        Assert.Contains(medicoes, m => m.Tag("resultado") == "sucesso");
        Assert.Contains(medicoes, m => m.Tag("resultado") == "falha");
    }

    // ── Tracing ──────────────────────────────────────────────────────────────

    [Fact]
    public void Iniciar_ComOuvinteAtivo_AbreUmSpanComONomeInformado()
    {
        // Arrange
        _coletor.Limpar();

        // Act
        using (var atividade = VetlyTelemetry.Iniciar("teste.operacao"))
        {
            Assert.NotNull(atividade);
            atividade.SetTag("vetly.teste", "valor");
        }

        // Assert — a asserção acontece depois do using: é o Dispose que encerra o span
        // e dispara o ActivityStopped que o coletor escuta.
        var span = _coletor.SpansChamados("teste.operacao").Single();

        Assert.Equal("valor", span.GetTagItem("vetly.teste"));
        Assert.True(span.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public void Iniciar_SpanFilho_HerdaOTraceDoPai()
    {
        // Arrange — é isto que faz um "trace distribuído" ser distribuído: o filho
        // carrega o mesmo TraceId e aponta para o pai. Sem essa herança, o backend
        // mostraria spans soltos em vez de uma árvore.
        _coletor.Limpar();

        // Act
        using (var pai = VetlyTelemetry.Iniciar("teste.pai"))
        using (var filho = VetlyTelemetry.Iniciar("teste.filho"))
        {
            Assert.NotNull(pai);
            Assert.NotNull(filho);

            Assert.Equal(pai.TraceId, filho.TraceId);
            Assert.Equal(pai.SpanId, filho.Parent?.SpanId);
        }

        // Assert
        Assert.Single(_coletor.SpansChamados("teste.pai"));
        Assert.Single(_coletor.SpansChamados("teste.filho"));
    }

    [Fact]
    public void RegistrarFalha_MarcaOSpanComoErroEAnexaAExcecao()
    {
        // Arrange — sem isto, um 422 sai como span verde: do ponto de vista do
        // transporte, a resposta foi um sucesso. Quem investiga precisa ver onde, dentro
        // da cadeia, a operação virou.
        _coletor.Limpar();
        var falha = new InvalidOperationException("horario ja reservado");

        // Act
        using (var atividade = VetlyTelemetry.Iniciar("teste.falha"))
            VetlyTelemetry.RegistrarFalha(atividade, falha);

        // Assert
        var span = _coletor.SpansChamados("teste.falha").Single();

        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal("horario ja reservado", span.StatusDescription);
        Assert.Contains(span.Events, e => e.Name == "exception");
    }

    [Fact]
    public void RegistrarFalha_ComSpanNulo_NaoLanca()
    {
        // Arrange — StartActivity devolve null quando ninguém escuta, e esse é o caminho
        // NORMAL em produção com amostragem baixa. Instrumentação que explode sem
        // listener derrubaria a aplicação justamente onde ela deveria ser invisível.
        Activity? inexistente = null;

        // Act
        var excecao = Record.Exception(
            () => VetlyTelemetry.RegistrarFalha(inexistente, new Exception("qualquer")));

        // Assert
        Assert.Null(excecao);
    }
}
