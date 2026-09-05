using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Vetly.IntegrationTests;

/// <summary>
/// A jornada inteira por HTTP, com os adaptadores simulados (§4).
///
/// Os testes de unidade provam cada regra isolada; este prova que elas se encaixam.
/// É o teste que pega o que nenhum outro pega: o campo que o serviço preenche e o
/// controller não devolve, a rota que existe mas não aceita o payload que a anterior
/// produziu, a guarda que barra o caminho legítimo por um id que veio errado do passo
/// anterior. Uma API pode ter todas as regras certas e ainda assim não ser
/// atravessável.
///
/// O caminho feliz vai do cadastro à avaliação. Os caminhos tristes cobrem os quatro
/// desfechos que custam dinheiro ou horário: checkout duplo, cancelamento por
/// terceiro, cancelamento com reembolso integral e pagamento recusado.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class JornadaCompletaTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>O mesmo valor de <c>appsettings.json</c>: a borda HTTP exige o token de serviço.</summary>
    private const string TokenDeServico = "DEFINA_UM_TOKEN_DE_SERVICO_LOCALMENTE";

    public JornadaCompletaTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string rota, string? token = null, string? json = null)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);

        if (json is not null)
            requisicao.Content = Corpo(json);

        if (token is not null)
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // As rotas que nao podem acontecer duas vezes exigem a chave. Uma nova por
        // chamada e o que o app faz: a chave protege contra o mesmo pedido reenviado,
        // e nao contra dois pedidos diferentes.
        if (metodo == HttpMethod.Post || metodo == HttpMethod.Delete)
            requisicao.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        return await _client.SendAsync(requisicao);
    }

    private async Task<JsonElement> LerAsync(HttpResponseMessage resposta, HttpStatusCode esperado)
    {
        var corpo = await resposta.Content.ReadAsStringAsync();

        Assert.True(resposta.StatusCode == esperado,
            $"Esperado {esperado}, veio {resposta.StatusCode}. Corpo: {corpo}");

        return string.IsNullOrWhiteSpace(corpo)
            ? default
            : JsonSerializer.Deserialize<JsonElement>(corpo, Json);
    }

    // ── Atores ───────────────────────────────────────────────────────────────

    private sealed record Responsavel(Guid TutorId, string Token, Guid AnimalId);

    private sealed record Prestador(Guid VeterinarioId, string Token, Guid ServicoId);

    /// <summary>Responsável cadastrado, com consentimento dado e um pet no nome dele.</summary>
    private async Task<Responsavel> CriarResponsavelAsync(string nome = "Ana")
    {
        var email = $"{nome.ToLowerInvariant()}-{Guid.NewGuid():N}@exemplo.com";

        var sessao = await LerAsync(await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"{{nome}} Teste","email":"{{email}}","telefone":"11999998888","senha":"senha-forte-123"}
            """)), HttpStatusCode.Created);

        var token = sessao.GetProperty("token").GetString()!;
        var tutorId = sessao.GetProperty("tutorId").GetGuid();

        // RN-060: a base legal precede o tratamento — sem consentimento, nada acontece
        Assert.True(sessao.GetProperty("consentimentoPendente").GetBoolean());

        await LerAsync(await EnviarAsync(HttpMethod.Put, $"/api/tutores/{tutorId}/consentimentos", token,
            """{"consentimentos":[{"finalidade":"Atendimento","concedido":true}]}"""), HttpStatusCode.OK);

        var pet = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/animais", token,
            $$"""
            {"nome":"Thor","especie":"Canino","raca":"Golden Retriever",
             "dataNascimento":"2023-04-10T00:00:00Z","tutorId":"{{tutorId}}","pesoKg":31.5,
             "alergias":["Dipirona"]}
            """), HttpStatusCode.Created);

        return new Responsavel(tutorId, token, pet.GetProperty("id").GetGuid());
    }

    /// <summary>Veterinário publicado no matching, com agenda materializada e vitrine.</summary>
    private async Task<Prestador> CriarPrestadorAsync()
    {
        // Cadastro de profissional é ato do Admin (RN-107): o token de desenvolvimento
        // é o caminho que o ambiente sem back-office oferece.
        var admin = await LerAsync(await _client.PostAsync("/api/auth/token", Corpo(
            """{"usuario":"admin-e2e","role":"Admin"}""")), HttpStatusCode.OK);

        var tokenAdmin = admin.GetProperty("token").GetString()!;

        var crmv = $"{Random.Shared.Next(10000, 99999)}-SP";
        var email = $"vet-{Guid.NewGuid():N}@exemplo.com";

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

        // RN-034: sem agenda materializada não há horário para o Responsável escolher
        await LerAsync(await EnviarAsync(HttpMethod.Put, $"/api/veterinarios/{vetId}/agenda-config", token,
            """
            {"dias":[1,2,3,4,5],"horaInicio":"08:00","horaFim":"18:00",
             "duracaoMinutos":30,"intervaloMinutos":0}
            """), HttpStatusCode.OK);

        // RN-032: o preço do checkout vem daqui, nunca do corpo da requisição
        var servicos = await LerAsync(await EnviarAsync(HttpMethod.Put, $"/api/veterinarios/{vetId}/servicos", token,
            """{"servicos":[{"tipo":1,"valor":200.00,"duracaoMinutos":30,"aceitaPlanoPet":false}]}"""),
            HttpStatusCode.OK);

        return new Prestador(vetId, token, servicos.EnumerateArray().First().GetProperty("id").GetGuid());
    }

    private sealed record Horario(Guid Id, DateTime Inicio);

    /// <summary>
    /// O primeiro horário livre da agenda do prestador, opcionalmente a partir de uma
    /// antecedência mínima — a faixa de reembolso depende de quão longe o atendimento
    /// está (RN-014), e o teste que a exercita precisa escolher o horário certo.
    /// </summary>
    private async Task<Horario> PrimeiroHorarioLivreAsync(
        Prestador prestador, string token, double antecedenciaEmHoras = 0)
    {
        var disponibilidade = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/veterinarios/{prestador.VeterinarioId}/disponibilidade", token),
            HttpStatusCode.OK);

        var limite = DateTime.UtcNow.AddHours(antecedenciaEmHoras);

        var horario = disponibilidade.GetProperty("dias").EnumerateArray()
            .SelectMany(d => d.GetProperty("horarios").EnumerateArray())
            .Select(h => new Horario(h.GetProperty("id").GetGuid(), h.GetProperty("inicio").GetDateTime()))
            .FirstOrDefault(h => h.Inicio > limite);

        Assert.True(horario is not null, "A agenda materializada nao devolveu horario livre no periodo pedido.");

        return horario!;
    }

    /// <summary>Faz o provedor simulado devolver o desfecho do pagamento pelo webhook.</summary>
    private async Task<JsonElement> WebhookAsync(string referenciaExterna, string status)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/internos/pagamentos/webhook")
        {
            Content = Corpo($$"""{"referenciaExterna":"{{referenciaExterna}}","status":"{{status}}"}""")
        };
        requisicao.Headers.Add("X-Vetly-Service-Token", TokenDeServico);

        return await LerAsync(await _client.SendAsync(requisicao), HttpStatusCode.OK);
    }

    /// <summary>Leva a consulta do checkout ao pagamento confirmado.</summary>
    private async Task<(Guid ConsultaId, Guid PagamentoId)> ConsultaConfirmadaAsync(
        Responsavel dono, Prestador prestador, double antecedenciaEmHoras = 0)
    {
        var slotId = (await PrimeiroHorarioLivreAsync(prestador, dono.Token, antecedenciaEmHoras)).Id;

        var checkout = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/consultas/checkout", dono.Token,
            $$"""
            {"animalId":"{{dono.AnimalId}}","prestadorId":"{{prestador.VeterinarioId}}",
             "slotId":"{{slotId}}","servicoId":"{{prestador.ServicoId}}"}
            """), HttpStatusCode.Created);

        var consultaId = checkout.GetProperty("consultaId").GetGuid();

        var cobranca = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/pagamentos", dono.Token,
            $$"""
            {"tutorId":"{{dono.TutorId}}","consultaId":"{{consultaId}}","valor":200.00,"meioPagamento":1}
            """), HttpStatusCode.Accepted); // 202: quem confirma e o webhook, nunca a resposta sincrona

        // RN-032: o valor cobrado é o do catálogo, e não o que o cliente mandou
        Assert.Equal(200.00m, cobranca.GetProperty("valor").GetDecimal());

        var pagamentoId = cobranca.GetProperty("id").GetGuid();
        var referencia = cobranca.GetProperty("instrucoes").GetProperty("referenciaExterna").GetString()!;

        var resultado = await WebhookAsync(referencia, "Confirmado");

        Assert.Equal("Confirmada", resultado.GetProperty("statusConsulta").GetString());

        return (consultaId, pagamentoId);
    }

    // ── Caminho feliz ────────────────────────────────────────────────────────

    [Fact]
    public async Task JornadaFeliz_DoCadastroAAvaliacao()
    {
        var dono = await CriarResponsavelAsync();
        var prestador = await CriarPrestadorAsync();

        var (consultaId, _) = await ConsultaConfirmadaAsync(dono, prestador);

        // ── Pré-sintomas: o único relato de quem convive com o animal (RN-005) ──
        await LerAsync(await EnviarAsync(HttpMethod.Put, $"/api/consultas/{consultaId}/pre-sintomas", dono.Token,
            """
            {"queixaPrincipal":"Vomito ha dois dias","duracaoEmDias":2,
             "sinaisObservados":["Apatia"],"alimentacaoNormal":false}
            """), HttpStatusCode.NoContent);

        // ── Briefing: o contexto chega ao profissional antes de ele começar ──
        var briefing = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/consultas/{consultaId}/briefing", prestador.Token),
            HttpStatusCode.OK);

        Assert.Equal(31.5m, briefing.GetProperty("pesoKg").GetDecimal());
        Assert.Equal("Vomito ha dois dias",
            briefing.GetProperty("preSintomas").GetProperty("queixaPrincipal").GetString());
        Assert.Contains("Dipirona",
            briefing.GetProperty("alergias").EnumerateArray().Select(a => a.GetString()));

        // ── Atendimento (RN-008) ──
        await LerAsync(await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/iniciar", prestador.Token),
            HttpStatusCode.OK);

        var encerrada = await LerAsync(
            await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/encerrar", prestador.Token),
            HttpStatusCode.OK);

        // P-01: encerrar marca Realizada; o fecho documental ainda não aconteceu
        Assert.Equal("Realizada", encerrada.GetProperty("statusConsulta").GetString());

        // ── Prontuário manual: sem áudio não há transcrição, e o caminho tem de
        // existir mesmo assim (RN-085) ──
        await LerAsync(await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/prontuario-manual",
            prestador.Token,
            """
            {"conteudo":{"anamnese":"Vomito ha dois dias, sem diarreia.",
             "exameFisico":"Hidratado, mucosas normocoradas, abdome sem dor a palpacao.",
             "hipotesesDiagnosticas":["Gastrite alimentar"],
             "conduta":"Dieta branda por cinco dias.",
             "orientacoes":"Retornar se o vomito persistir."}}
            """), HttpStatusCode.OK);

        // ── Documento gerado, assinado e publicado (RN-083/RN-087/RN-090) ──
        // O tipo vai na query: a factory e escolhida por ele (RN-083)
        var documento = await LerAsync(await EnviarAsync(HttpMethod.Post,
            $"/api/documentos/consulta/{consultaId}?tipo=Prontuario", prestador.Token, "{}"),
            HttpStatusCode.Created);

        var documentoId = documento.GetProperty("id").GetGuid();

        // RN-087: a assinatura e ato nominal do profissional, e nao um clique
        await LerAsync(await EnviarAsync(HttpMethod.Post, $"/api/documentos/{documentoId}/assinar",
            prestador.Token, """{"nomeCompleto":"Dra. Marina"}"""), HttpStatusCode.OK);

        var publicado = await LerAsync(await EnviarAsync(HttpMethod.Post,
            $"/api/documentos/{documentoId}/publicar", prestador.Token, "{}"), HttpStatusCode.OK);

        Assert.NotEqual(JsonValueKind.Null, publicado.GetProperty("publicadoEm").ValueKind);

        // ── Fecho documental (RN-087) e fecho do ciclo de captura (§7.3) ──
        var finalizada = await LerAsync(await EnviarAsync(HttpMethod.Post,
            $"/api/consultas/{consultaId}/finalizar", prestador.Token), HttpStatusCode.OK);

        Assert.True(finalizada.GetProperty("finalizada").GetBoolean());

        // Como o veterinario escolhe quais documentos emitir, nenhuma automacao declara
        // o ciclo fechado: quem fecha e este ato. Sem ele a sessao ficava em
        // Documentando para sempre e o app nunca saia do polling
        Assert.Equal("Concluida", finalizada.GetProperty("estadoDaSessao").GetString());

        // ── O Responsável lê no board do pet (RN-090) ──
        var lido = await LerAsync(await EnviarAsync(HttpMethod.Post, $"/api/documentos/{documentoId}/lido",
            dono.Token, "{}"), HttpStatusCode.OK);

        Assert.NotEqual(JsonValueKind.Null, lido.GetProperty("lidoEm").ValueKind);

        // ── Avaliação: só avalia quem foi atendido (RN-055) ──
        var avaliacao = await LerAsync(await EnviarAsync(HttpMethod.Post,
            $"/api/avaliacoes/consulta/{consultaId}", dono.Token,
            """{"nota":5,"comentario":"Atendimento atencioso."}"""), HttpStatusCode.Created);

        Assert.Equal(5, avaliacao.GetProperty("nota").GetInt32());
    }

    [Fact]
    public async Task JornadaFeliz_RetornoNasceConfirmadoESemCobranca()
    {
        var dono = await CriarResponsavelAsync();
        var prestador = await CriarPrestadorAsync();

        var (consultaId, _) = await ConsultaConfirmadaAsync(dono, prestador);

        await LerAsync(await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/iniciar", prestador.Token),
            HttpStatusCode.OK);
        await LerAsync(await EnviarAsync(HttpMethod.Post, $"/api/consultas/{consultaId}/encerrar", prestador.Token),
            HttpStatusCode.OK);

        var slotDoRetorno = (await PrimeiroHorarioLivreAsync(prestador, prestador.Token)).Id;

        var retorno = await LerAsync(await EnviarAsync(HttpMethod.Post,
            $"/api/consultas/{consultaId}/retorno", prestador.Token,
            $$"""{"slotId":"{{slotDoRetorno}}","motivo":"Revisar a resposta a dieta"}"""),
            HttpStatusCode.Created);

        var retornoId = retorno.GetProperty("consultaId").GetGuid();
        Assert.Equal(consultaId, retorno.GetProperty("consultaOrigemId").GetGuid());

        var consultaDoRetorno = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/consultas/{retornoId}", dono.Token), HttpStatusCode.OK);

        // RN-013: o retorno e a segunda metade de um tratamento ja pago
        Assert.Equal("Confirmada", consultaDoRetorno.GetProperty("status").GetString());
        Assert.Equal("Confirmado", consultaDoRetorno.GetProperty("statusPagamento").GetString());
    }

    // ── Caminhos tristes ─────────────────────────────────────────────────────

    [Fact]
    public async Task CheckoutDuplo_NoMesmoHorario_Retorna409()
    {
        var ana = await CriarResponsavelAsync("Ana");
        var bruno = await CriarResponsavelAsync("Bruno");
        var prestador = await CriarPrestadorAsync();

        var slotId = (await PrimeiroHorarioLivreAsync(prestador, ana.Token)).Id;

        await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/consultas/checkout", ana.Token,
            $$"""
            {"animalId":"{{ana.AnimalId}}","prestadorId":"{{prestador.VeterinarioId}}",
             "slotId":"{{slotId}}","servicoId":"{{prestador.ServicoId}}"}
            """), HttpStatusCode.Created);

        var segunda = await EnviarAsync(HttpMethod.Post, "/api/consultas/checkout", bruno.Token,
            $$"""
            {"animalId":"{{bruno.AnimalId}}","prestadorId":"{{prestador.VeterinarioId}}",
             "slotId":"{{slotId}}","servicoId":"{{prestador.ServicoId}}"}
            """);

        // RN-035: um unico ponto decide quem fica com o horario; quem chega depois
        // escolhe outro
        Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
    }

    [Fact]
    public async Task CancelamentoPorTerceiro_Retorna403()
    {
        var ana = await CriarResponsavelAsync("Ana");
        var bruno = await CriarResponsavelAsync("Bruno");
        var prestador = await CriarPrestadorAsync();

        var (consultaId, _) = await ConsultaConfirmadaAsync(ana, prestador);

        var resposta = await EnviarAsync(HttpMethod.Delete, $"/api/consultas/{consultaId}", bruno.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task CancelamentoComMaisDe24h_ReembolsaIntegralELiberaOHorario()
    {
        var dono = await CriarResponsavelAsync();
        var prestador = await CriarPrestadorAsync();

        // A faixa de reembolso depende da antecedencia (RN-014): o horario e escolhido
        // deliberadamente a mais de 24h para exercitar a devolucao integral.
        var (consultaId, _) = await ConsultaConfirmadaAsync(dono, prestador, antecedenciaEmHoras: 30);

        var consulta = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/consultas/{consultaId}", dono.Token), HttpStatusCode.OK);

        var horario = consulta.GetProperty("dataHora").GetDateTime();

        Assert.True(horario > DateTime.UtcNow.AddHours(24));

        var resultado = await LerAsync(
            await EnviarAsync(HttpMethod.Delete, $"/api/consultas/{consultaId}", dono.Token), HttpStatusCode.OK);

        // RN-014: acima de 24h o reembolso e integral
        Assert.Equal(200.00m, resultado.GetProperty("valorReembolso").GetDecimal());
        Assert.Equal(0m, resultado.GetProperty("percentualRetencao").GetDecimal());

        // RN-037: o horario volta a valer para quem estava esperando
        var disponibilidade = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/veterinarios/{prestador.VeterinarioId}/disponibilidade",
                dono.Token), HttpStatusCode.OK);

        var livres = disponibilidade.GetProperty("dias").EnumerateArray()
            .SelectMany(d => d.GetProperty("horarios").EnumerateArray())
            .Select(h => h.GetProperty("inicio").GetDateTime());

        Assert.Contains(horario, livres);
    }

    [Fact]
    public async Task PagamentoRecusado_ExpiraAConsultaELiberaOHorario()
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
        var horario = checkout.GetProperty("resumo").GetProperty("dataHora").GetDateTime();

        var cobranca = await LerAsync(await EnviarAsync(HttpMethod.Post, "/api/pagamentos", dono.Token,
            $$"""
            {"tutorId":"{{dono.TutorId}}","consultaId":"{{consultaId}}","valor":200.00,"meioPagamento":1}
            """), HttpStatusCode.Accepted); // 202: quem confirma e o webhook, nunca a resposta sincrona

        var referencia = cobranca.GetProperty("instrucoes").GetProperty("referenciaExterna").GetString()!;

        var resultado = await WebhookAsync(referencia, "Recusado");

        // Segurar o horario de quem nao pagou tiraria a vaga de quem pagaria (RN-035)
        Assert.Equal("Expirada", resultado.GetProperty("statusConsulta").GetString());

        var disponibilidade = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/veterinarios/{prestador.VeterinarioId}/disponibilidade",
                dono.Token), HttpStatusCode.OK);

        var livres = disponibilidade.GetProperty("dias").EnumerateArray()
            .SelectMany(d => d.GetProperty("horarios").EnumerateArray())
            .Select(h => h.GetProperty("inicio").GetDateTime());

        Assert.Contains(horario, livres);
    }

    // ── A carteira do Responsavel (RN-106) ─────────────────────────────────

    [Fact]
    public async Task Carteira_MostraOPagamentoConfirmadoDaConsulta()
    {
        var dono = await CriarResponsavelAsync();
        var prestador = await CriarPrestadorAsync();

        var (consultaId, pagamentoId) = await ConsultaConfirmadaAsync(dono, prestador);

        var carteira = await LerAsync(
            await EnviarAsync(HttpMethod.Get, $"/api/tutores/{dono.TutorId}/carteira", dono.Token),
            HttpStatusCode.OK);

        var lancamentos = carteira.GetProperty("lancamentos").EnumerateArray().ToList();

        Assert.Contains(lancamentos, l => l.GetProperty("pagamentoId").GetGuid() == pagamentoId
                                          && l.GetProperty("consultaId").GetGuid() == consultaId
                                          && l.GetProperty("status").GetString() == "Confirmado");

        // So transacao confirmada soma no total pago
        Assert.Equal(200.00m, carteira.GetProperty("totalPago").GetDecimal());
    }

    [Fact]
    public async Task Carteira_DeOutroResponsavel_Retorna403()
    {
        var ana = await CriarResponsavelAsync("Ana");
        var bruno = await CriarResponsavelAsync("Bruno");

        var resposta = await EnviarAsync(HttpMethod.Get, $"/api/tutores/{bruno.TutorId}/carteira", ana.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }
}
