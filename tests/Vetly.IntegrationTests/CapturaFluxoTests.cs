using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// O ciclo de captura inteiro por HTTP, com o motor simulado (§4.2, §5.3).
///
/// Os testes de unidade provam cada transição isolada; este prova que a sessão
/// <b>sempre chega a um estado terminal</b>. É o que o app precisa: ele faz polling de
/// <c>GET /api/consultas/{id}/rascunho</c>, e uma sessão que fica parada em
/// <c>AguardandoTranscricao</c> não é um erro que ele consiga mostrar — é uma tela que
/// nunca sai do lugar.
///
/// Os três desfechos possíveis estão cobertos: todos os trechos transcrevem, parte
/// transcreve, nenhum transcreve.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class CapturaFluxoTests
{
    private readonly HttpClient _client;
    private readonly VetlyWebApplicationFactory _factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>O mesmo valor de <c>appsettings.json</c>.</summary>
    private const string TokenDeServico = "DEFINA_UM_TOKEN_DE_SERVICO_LOCALMENTE";

    public CapturaFluxoTests(VetlyWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Os três desfechos da transcrição (§7.3) ──────────────────────────────

    [Fact]
    public async Task Captura_TodosOsTrechosTranscrevem_SessaoVaiParaGerandoRascunho()
    {
        var (dono, prestador, consultaId) = await ConsultaPronataParaAtenderAsync();

        await IniciarAsync(consultaId, prestador);
        await EnviarSegmentosAsync(consultaId, dono, prestador, quantidade: 3);
        await RodarWorkerAsync();

        var encerrada = await LerAsync(
            await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/encerrar", prestador.Token),
            HttpStatusCode.OK);

        Assert.Equal("GerandoRascunho", encerrada.GetProperty("estadoDaSessao").GetString());

        var captura = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/consultas/{consultaId}/captura", prestador.Token),
            HttpStatusCode.OK);

        Assert.Equal(3, captura.GetProperty("segmentosRecebidos").GetInt32());
        Assert.Equal(3, captura.GetProperty("segmentosTranscritos").GetInt32());
        Assert.Equal(0, captura.GetProperty("segmentosComFalha").GetInt32());

        // O texto parcial sai na ordem dos trechos: e o que a barra de progresso mostra
        // e o que alimenta a estruturacao (RN-080)
        var texto = captura.GetProperty("textoParcial").GetString()!;

        Assert.Contains("trecho 0", texto);
        Assert.Contains("trecho 2", texto);
        Assert.True(texto.IndexOf("trecho 0", StringComparison.Ordinal) <
                    texto.IndexOf("trecho 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Captura_UmTrechoPerdido_SessaoVaiParaTranscricaoParcial()
    {
        var (dono, prestador, consultaId) = await ConsultaPronataParaAtenderAsync();

        await IniciarAsync(consultaId, prestador);
        var segmentos = await EnviarSegmentosAsync(consultaId, dono, prestador, quantidade: 3);

        // O trecho do meio esgota as tentativas antes de qualquer callback chegar
        await DarTrechoComoPerdidoAsync(segmentos[1]);
        await RodarWorkerAsync();

        var encerrada = await LerAsync(
            await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/encerrar", prestador.Token),
            HttpStatusCode.OK);

        // Perder a consulta inteira porque um trecho falhou seria pior que um rascunho
        // parcial: o rascunho sai com o que ha, e com aviso (RN-082)
        Assert.Equal("TranscricaoParcial", encerrada.GetProperty("estadoDaSessao").GetString());

        var captura = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/consultas/{consultaId}/captura", prestador.Token),
            HttpStatusCode.OK);

        Assert.Equal(2, captura.GetProperty("segmentosTranscritos").GetInt32());
        Assert.Equal(1, captura.GetProperty("segmentosComFalha").GetInt32());

        var perdido = captura.GetProperty("segmentos").EnumerateArray()
            .Single(s => s.GetProperty("id").GetGuid() == segmentos[1]);

        Assert.Equal("Timeout", perdido.GetProperty("falhaMotivo").GetString());

        // O texto que sobrou continua utilizavel — e e nele que a estruturacao trabalha
        Assert.Contains("trecho 0", captura.GetProperty("textoParcial").GetString());
    }

    [Fact]
    public async Task Captura_NenhumTrechoTranscreve_SessaoVaiParaSemTranscricaoEOManualFunciona()
    {
        var (dono, prestador, consultaId) = await ConsultaPronataParaAtenderAsync();

        await IniciarAsync(consultaId, prestador);
        var segmentos = await EnviarSegmentosAsync(consultaId, dono, prestador, quantidade: 3);

        foreach (var segmento in segmentos)
            await DarTrechoComoPerdidoAsync(segmento);

        await RodarWorkerAsync();

        var encerrada = await LerAsync(
            await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/encerrar", prestador.Token),
            HttpStatusCode.OK);

        Assert.Equal("SemTranscricao", encerrada.GetProperty("estadoDaSessao").GetString());

        // Sem rascunho nao ha o que decidir: a rota devolve 404 e o app sabe que o
        // caminho e o manual (RN-085)
        var rascunho = await EnviarAsync(HttpMethod.Get, $"/api/consultas/{consultaId}/rascunho", prestador.Token);
        Assert.Equal(HttpStatusCode.NotFound, rascunho.StatusCode);

        // O atendimento aconteceu e precisa virar prontuario de algum jeito
        var manual = await LerAsync(await EnviarAsync(HttpMethod.Post,
            $"/api/consultas/{consultaId}/prontuario-manual", prestador.Token,
            """
            {"conteudo":{"anamnese":"Vomito ha dois dias, sem diarreia.",
             "exameFisico":"Hidratado, mucosas normocoradas.",
             "hipotesesDiagnosticas":["Gastrite alimentar"],
             "conduta":"Dieta branda por cinco dias.",
             "orientacoes":"Retornar se o vomito persistir."}}
            """), HttpStatusCode.OK);

        Assert.Equal("Manual", manual.GetProperty("decisao").GetString());
    }

    [Fact]
    public async Task Captura_TodosOsTrechosPresosEmRecebido_AVarreduraLevaASessaoAoTerminal()
    {
        var (dono, prestador, consultaId) = await ConsultaPronataParaAtenderAsync();

        await IniciarAsync(consultaId, prestador);
        var segmentos = await EnviarSegmentosAsync(consultaId, dono, prestador, quantidade: 3);

        // O outro jeito de travar: o job de despacho esgotou as tentativas DELE e
        // morreu, deixando o trecho em `Recebido` sem ninguem para reenfileira-lo. O
        // worker de proposito NAO roda aqui — e exatamente essa a situacao.
        foreach (var segmento in segmentos)
            await PrenderTrechoEmRecebidoAsync(segmento);

        var encerrada = await LerAsync(
            await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/encerrar", prestador.Token),
            HttpStatusCode.OK);

        // A sessao esta PRESA: nenhum trecho tem desfecho, e o desfecho da sessao so e
        // avaliado quando todos responderam. Sem a varredura, e aqui que ela fica para
        // sempre — e o app segue no polling infinito do rascunho.
        Assert.Equal("AguardandoTranscricao", encerrada.GetProperty("estadoDaSessao").GetString());
        Assert.Equal(3, encerrada.GetProperty("segmentosPendentes").GetInt32());

        await RodarVarreduraDeTravadosAsync();

        var captura = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/consultas/{consultaId}/captura", prestador.Token),
            HttpStatusCode.OK);

        // A garantia da §4.2 nao e "Enviado nao trava", e "a sessao SEMPRE chega a um
        // estado terminal" — por qualquer das portas
        Assert.Equal("SemTranscricao", captura.GetProperty("estado").GetString());
        Assert.Equal(3, captura.GetProperty("segmentosComFalha").GetInt32());

        Assert.All(captura.GetProperty("segmentos").EnumerateArray(),
            s => Assert.Equal("Timeout", s.GetProperty("falhaMotivo").GetString()));

        // Estado terminal tambem do lado do app: 404 e resposta definitiva — nao ha
        // rascunho e nao vai haver —, e nao "ainda estou processando"
        var rascunho = await EnviarAsync(HttpMethod.Get, $"/api/consultas/{consultaId}/rascunho", prestador.Token);
        Assert.Equal(HttpStatusCode.NotFound, rascunho.StatusCode);

        // E o caminho manual, que e o desfecho previsto, esta aberto (RN-085)
        var manual = await LerAsync(await EnviarAsync(HttpMethod.Post,
            $"/api/consultas/{consultaId}/prontuario-manual", prestador.Token,
            """
            {"conteudo":{"anamnese":"Vomito ha dois dias.","exameFisico":"Sem alteracoes.",
             "hipotesesDiagnosticas":["Gastrite alimentar"],"conduta":"Dieta branda.",
             "orientacoes":"Retornar se persistir."}}
            """), HttpStatusCode.OK);

        Assert.Equal("Manual", manual.GetProperty("decisao").GetString());
    }

    // ── Parametros de gravacao entregues ao front (Parte 5) ──────────────────

    [Fact]
    public async Task Iniciar_EntregaAoFrontUmFormatoQueOMotorConsegueLer()
    {
        var (_, prestador, consultaId) = await ConsultaPronataParaAtenderAsync();

        var sessao = await IniciarAsync(consultaId, prestador);
        var gravacao = sessao.GetProperty("gravacao");

        // WebM porque e o que o MediaRecorder do Chromium grava, e a Fast Transcription
        // do Azure le nativamente. A instrucao anterior era OGG, que so o Firefox grava:
        // na pratica a captura nao funcionava no navegador da clinica
        Assert.Equal("audio/webm;codecs=opus", gravacao.GetProperty("formato").GetString());
        Assert.Equal(16000, gravacao.GetProperty("sampleRate").GetInt32());
        Assert.Equal(30, gravacao.GetProperty("segundosPorSegmento").GetInt32());
    }

    [Fact]
    public async Task Iniciar_ListaOsFormatosAceitosEmOrdemDePreferencia()
    {
        var (_, prestador, consultaId) = await ConsultaPronataParaAtenderAsync();

        var sessao = await IniciarAsync(consultaId, prestador);
        var gravacao = sessao.GetProperty("gravacao");

        var aceitos = gravacao.GetProperty("formatosAceitos")
            .EnumerateArray().Select(f => f.GetString()).ToList();

        // Nenhum formato unico cobre todos os navegadores: a lista existe para o front
        // negociar com MediaRecorder.isTypeSupported() em vez de fixar no codigo a
        // escolha que so vale no navegador em que foi testado
        Assert.Equal(gravacao.GetProperty("formato").GetString(), aceitos.First());
        Assert.Contains("audio/ogg;codecs=opus", aceitos);
        Assert.Contains("audio/wav", aceitos);
    }

    [Fact]
    public async Task Midia_AceitaTodosOsFormatosQueOsParametrosDeGravacaoOferecem()
    {
        var (_, prestador, consultaId) = await ConsultaPronataParaAtenderAsync();
        var dono = await CriarResponsavelAsync();

        var gravacao = (await IniciarAsync(consultaId, prestador)).GetProperty("gravacao");

        var aceitos = gravacao.GetProperty("formatosAceitos")
            .EnumerateArray().Select(f => f.GetString()!).ToList();

        // Todo formato que a API manda gravar tem de ser aceito no upload: oferecer um
        // que o upload recusa deixaria o front sem conseguir nem enviar o trecho
        foreach (var formato in aceitos)
        {
            var url = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/midia/upload-url", dono.Token,
                $$"""{"tipo":"AudioConsulta","contentType":"{{formato}}"}"""), HttpStatusCode.Created);

            Assert.NotEqual(Guid.Empty, url.GetProperty("midiaId").GetGuid());
            Assert.Equal(formato, url.GetProperty("contentType").GetString());
        }
    }

    // ── Andaimes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Roda a fila até drenar, como o worker faria — só que sem esperar os ciclos de
    /// 30s e as esperas dos jobs agendados.
    /// </summary>
    private async Task RodarWorkerAsync(int ciclos = 4)
    {
        for (var i = 0; i < ciclos; i++)
        {
            using var escopo = _factory.Services.CreateScope();

            var fila = escopo.ServiceProvider.GetRequiredService<IFilaDeJobs>();
            var handlers = escopo.ServiceProvider.GetServices<IJobHandler>().ToDictionary(h => h.Tipo);

            // "Agora" no futuro: o motor simulado agenda o callback com atraso, e esperar
            // esse atraso de verdade so faria a suite demorar
            var elegiveis = await fila.ObterElegiveisAsync(DateTime.UtcNow.AddMinutes(10), 50);

            if (elegiveis.Count == 0)
                return;

            foreach (var job in elegiveis)
            {
                try
                {
                    if (handlers.TryGetValue(job.Tipo, out var handler))
                        await handler.ExecutarAsync(job, CancellationToken.None);

                    job.Concluir();
                }
                catch (Exception ex)
                {
                    // Mesma politica do worker: falha nao derruba o ciclo. A estruturacao
                    // pela IA, por exemplo, falha aqui porque nao ha Ollama na suite — e
                    // isso nao pode impedir os outros jobs de rodar.
                    job.RegistrarFalha(ex.Message, DateTime.UtcNow);
                }
            }

            await fila.SalvarAsync();
        }
    }

    /// <summary>
    /// Roda a varredura de trechos travados pelo mesmo handler que o worker executa.
    /// </summary>
    private async Task RodarVarreduraDeTravadosAsync()
    {
        using var escopo = _factory.Services.CreateScope();

        var handler = escopo.ServiceProvider.GetServices<IJobHandler>()
            .Single(h => h.Tipo == TipoJob.VerificarTranscricaoTravada);

        await handler.ExecutarAsync(
            new Vetly.Domain.Entities.Job(TipoJob.VerificarTranscricaoTravada), CancellationToken.None);
    }

    /// <summary>
    /// Deixa o trecho em <c>Recebido</c>, fora do prazo e a uma tentativa do limite —
    /// o estado em que um job de despacho que morreu abandona o segmento.
    /// </summary>
    private async Task PrenderTrechoEmRecebidoAsync(Guid segmentoId)
    {
        using var escopo = _factory.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<VetlyDbContext>();

        var segmento = await contexto.SegmentosDeAudio.FirstAsync(s => s.Id == segmentoId);

        for (var i = 0; i < Vetly.Domain.Entities.SegmentoAudio.MaximoDeTentativas - 1; i++)
            segmento.RegistrarDespacho(new string('a', 64), DateTime.UtcNow.AddMinutes(-10));

        // Motor recusou o despacho: volta para `Recebido` sem consumir tentativa, e o
        // job que deveria retentar e o que morreu
        segmento.RegistrarFalha(MotivoFalhaTranscricao.MotorIndisponivel);

        Assert.Equal(EstadoSegmentoAudio.Recebido, segmento.Estado);

        await contexto.SaveChangesAsync();
    }

    /// <summary>
    /// Esgota as tentativas de um trecho e o marca como perdido por timeout — o estado
    /// em que a varredura da §4.2 deixa um segmento cujo callback nunca voltou.
    /// </summary>
    private async Task DarTrechoComoPerdidoAsync(Guid segmentoId)
    {
        using var escopo = _factory.Services.CreateScope();
        var contexto = escopo.ServiceProvider.GetRequiredService<VetlyDbContext>();

        var segmento = await contexto.SegmentosDeAudio.FirstAsync(s => s.Id == segmentoId);

        while (segmento.Tentativas < Vetly.Domain.Entities.SegmentoAudio.MaximoDeTentativas)
            segmento.RegistrarDespacho(new string('a', 64), DateTime.UtcNow.AddMinutes(-10));

        segmento.RegistrarFalha(MotivoFalhaTranscricao.Timeout);

        await contexto.SaveChangesAsync();
    }

    private async Task<JsonElement> IniciarAsync(Guid consultaId, Prestador prestador) =>
        await LerAsync(
            await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/iniciar", prestador.Token),
            HttpStatusCode.OK);

    /// <summary>Envia N trechos de áudio pela mesma rota que o app usa, e devolve os ids.</summary>
    private async Task<List<Guid>> EnviarSegmentosAsync(
        Guid consultaId, Responsavel dono, Prestador prestador, int quantidade)
    {
        var segmentos = new List<Guid>();

        for (var i = 0; i < quantidade; i++)
        {
            var midia = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/midia/upload-url", dono.Token,
                $$"""
                {"tipo":"AudioConsulta","contentType":"audio/ogg;codecs=opus","consultaId":"{{consultaId}}"}
                """), HttpStatusCode.Created);

            var recebido = await LerAsync(await EnviarAsync(HttpMethod.Post,
                $"/api/consultas/{consultaId}/captura/segmentos", prestador.Token,
                $$"""
                {"sequencia":{{i}},"midiaId":"{{midia.GetProperty("midiaId").GetGuid()}}",
                 "duracaoMs":30000,"inicioRelativoMs":{{i * 30000}}}
                """), HttpStatusCode.Accepted);

            segmentos.Add(recebido.GetProperty("segmentoId").GetGuid());
        }

        return segmentos;
    }

    // ── Atores (mesmos passos da jornada completa) ───────────────────────────

    private sealed record Responsavel(Guid TutorId, string Token, Guid AnimalId);

    private sealed record Prestador(Guid VeterinarioId, string Token, Guid ServicoId);

    /// <summary>Consulta confirmada e paga, pronta para o veterinário iniciar.</summary>
    private async Task<(Responsavel Dono, Prestador Prestador, Guid ConsultaId)> ConsultaPronataParaAtenderAsync()
    {
        var dono = await CriarResponsavelAsync();
        var prestador = await CriarPrestadorAsync();

        var slotId = (await PrimeiroHorarioLivreAsync(prestador, dono.Token)).Id;

        var checkout = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/consultas/checkout", dono.Token,
            $$"""
            {"animalId":"{{dono.AnimalId}}","prestadorId":"{{prestador.VeterinarioId}}",
             "slotId":"{{slotId}}","servicoId":"{{prestador.ServicoId}}"}
            """), HttpStatusCode.Created);

        var consultaId = checkout.GetProperty("consultaId").GetGuid();

        var cobranca = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/pagamentos", dono.Token,
            $$"""
            {"tutorId":"{{dono.TutorId}}","consultaId":"{{consultaId}}","valor":200.00,"meioPagamento":1}
            """), HttpStatusCode.Accepted);

        var referencia = cobranca.GetProperty("instrucoes").GetProperty("referenciaExterna").GetString()!;

        var webhook = new HttpRequestMessage(HttpMethod.Post, "/api/internos/pagamentos/webhook")
        {
            Content = Corpo($$"""{"referenciaExterna":"{{referencia}}","status":"Confirmado"}""")
        };
        webhook.Headers.Add("X-Vetly-Service-Token", TokenDeServico);

        await LerAsync(await _client.SendAsync(webhook), HttpStatusCode.OK);

        return (dono, prestador, consultaId);
    }

    private async Task<Responsavel> CriarResponsavelAsync()
    {
        var email = $"captura-{Guid.NewGuid():N}@exemplo.com";

        var sessao = await LerAsync(await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"Ana Teste","email":"{{email}}","telefone":"11999998888","senha":"senha-forte-123"}
            """)), HttpStatusCode.Created);

        var token = sessao.GetProperty("token").GetString()!;
        var tutorId = sessao.GetProperty("tutorId").GetGuid();

        await LerAsync(await EnviarAsync(HttpMethod.Put, $"/api/tutores/{tutorId}/consentimentos", token,
            """{"consentimentos":[{"finalidade":"Atendimento","concedido":true}]}"""), HttpStatusCode.OK);

        var pet = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/animais", token,
            $$"""
            {"nome":"Thor","especie":"Canino","raca":"Golden Retriever",
             "dataNascimento":"2023-04-10T00:00:00Z","tutorId":"{{tutorId}}","pesoKg":31.5}
            """), HttpStatusCode.Created);

        return new Responsavel(tutorId, token, pet.GetProperty("id").GetGuid());
    }

    private async Task<Prestador> CriarPrestadorAsync()
    {
        var admin = await LerAsync(await _client.PostAsync("/api/auth/token", Corpo(
            """{"usuario":"admin-captura","role":"Admin"}""")), HttpStatusCode.OK);

        var tokenAdmin = admin.GetProperty("token").GetString()!;

        var crmv = $"{Random.Shared.Next(10000, 99999)}-SP";
        var email = $"vet-captura-{Guid.NewGuid():N}@exemplo.com";

        // Plano Profissional: no Basico nao ha captura de audio (RN-085)
        var vet = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/veterinarios", tokenAdmin,
            $$"""
            {"nome":"Dra. Marina","crmv":"{{crmv}}","ufAtuacao":"SP","email":"{{email}}",
             "persona":1,"plano":2}
            """), HttpStatusCode.Created);

        var vetId = vet.GetProperty("veterinario").GetProperty("id").GetGuid();
        var senhaTemporaria = vet.GetProperty("senhaTemporaria").GetString()!;

        var sessao = await LerAsync(await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{email}}","senha":"{{senhaTemporaria}}"}""")), HttpStatusCode.OK);

        var token = sessao.GetProperty("token").GetString()!;

        await LerAsync(await EnviarAsync(HttpMethod.Put, $"/api/veterinarios/{vetId}/agenda-config", token,
            """
            {"dias":[1,2,3,4,5],"horaInicio":"08:00","horaFim":"18:00",
             "duracaoMinutos":30,"intervaloMinutos":0}
            """), HttpStatusCode.OK);

        var servicos = await LerAsync(await EnviarAsync(HttpMethod.Put, $"/api/veterinarios/{vetId}/servicos", token,
            """{"servicos":[{"tipo":1,"valor":200.00,"duracaoMinutos":30,"aceitaPlanoPet":false}]}"""),
            HttpStatusCode.OK);

        return new Prestador(vetId, token, servicos.EnumerateArray().First().GetProperty("id").GetGuid());
    }

    private sealed record Horario(Guid Id, DateTime Inicio);

    private async Task<Horario> PrimeiroHorarioLivreAsync(Prestador prestador, string token)
    {
        var disponibilidade = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/veterinarios/{prestador.VeterinarioId}/disponibilidade", token),
            HttpStatusCode.OK);

        var horario = disponibilidade.GetProperty("dias").EnumerateArray()
            .SelectMany(d => d.GetProperty("horarios").EnumerateArray())
            .Select(h => new Horario(h.GetProperty("id").GetGuid(), h.GetProperty("inicio").GetDateTime()))
            .FirstOrDefault(h => h.Inicio > DateTime.UtcNow);

        Assert.True(horario is not null, "A agenda materializada nao devolveu horario livre.");

        return horario!;
    }

    // ── HTTP ─────────────────────────────────────────────────────────────────

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string rota, string? token = null, string? json = null)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);

        if (json is not null)
            requisicao.Content = Corpo(json);

        if (token is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (metodo == HttpMethod.Post || metodo == HttpMethod.Delete)
            requisicao.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        return await _client.SendAsync(requisicao);
    }

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.True(resposta.StatusCode == esperado,
            $"Esperado {esperado}, veio {resposta.StatusCode}. Corpo: {corpo}");

        return string.IsNullOrWhiteSpace(corpo)
            ? default
            : JsonSerializer.Deserialize<JsonElement>(corpo, Json);
    }
}
