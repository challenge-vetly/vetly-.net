using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Observability;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de integracao com o Ollama (LLM local).
/// Todos os retornos sao sugestoes de IA — o veterinario deve validar antes de qualquer acao clinica (RN-082).
/// </summary>
public class OllamaService : IOllamaService
{
    /// <summary>
    /// Teto de tokens da resposta nas operacoes de sugestao pontual.
    ///
    /// Todas devolvem um objeto pequeno — uma lista de hipoteses, um protocolo, uma
    /// triagem —, e 500 sobra para elas.
    /// </summary>
    private const int TokensDaResposta = 500;

    /// <summary>
    /// Teto proprio da estruturacao do prontuario (§5.4).
    ///
    /// O prontuario estruturado tem cinco campos e um deles e texto corrido: com 500
    /// tokens o JSON trunca no meio, o parse falha e o fallback despeja o texto bruto na
    /// anamnese. O custo aparece para o veterinario como "a IA nao estruturou" — que e o
    /// sintoma errado para o diagnostico certo, e manda procurar o problema no modelo em
    /// vez de no limite da resposta.
    /// </summary>
    private const int TokensDaEstruturacao = 1500;

    private readonly HttpClient _http;
    private readonly string _model;

    // Timeout de 120s configurado no HttpClient registrado no DI
    public OllamaService(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _model = configuration["Ollama:Model"] ?? "llama3.2";
    }

    /// <inheritdoc/>
    public async Task<List<HipoteseDiagnosticaDto>> SugerirDiagnosticoAsync(ContextoClinicoDto contexto)
    {
        var sintomasLista = string.Join(", ", contexto.Sintomas);
        var historico = contexto.HistoricoRelevante is not null
            ? $" Historico: {contexto.HistoricoRelevante}."
            : string.Empty;

        var prompt =
            $"Voce e um assistente veterinario. Com base no contexto clinico abaixo, liste ate 3 hipoteses diagnosticas " +
            $"em formato JSON com os campos: hipotese, nivelConfianca (baixo/medio/alto) e justificativa. " +
            $"Responda APENAS com o array JSON, sem texto adicional.\n\n" +
            $"Especie: {contexto.Especie}, Raca: {contexto.Raca}, Idade: {contexto.IdadeAnos} anos, Peso: {contexto.PesoKg} kg.\n" +
            $"Sintomas: {sintomasLista}.{historico}";

        var resposta = await EnviarAsync(prompt);

        // Tenta parsear como array de hipoteses; retorna lista vazia em caso de falha de parsing
        return ParsearListaOuVazio<HipoteseDiagnosticaDto>(resposta);
    }

    /// <inheritdoc/>
    public async Task<ProtocoloTratamentoDto> SugerirProtocoloAsync(string diagnostico, string especie, decimal pesoKg)
    {
        // RN-081: dose sem peso e onde mora o erro clinico. Sem peso cadastrado a IA nao e
        // sequer consultada — exigir o dado antes vale mais que devolver uma sugestao sem dose.
        if (pesoKg <= 0)
            throw new BusinessRuleException("RN-081",
                "O peso do animal e obrigatorio para sugerir posologia. Cadastre o peso do animal antes de solicitar o protocolo.");

        var prompt =
            $"Voce e um assistente veterinario. Sugira um protocolo de tratamento em formato JSON com os campos: " +
            $"diagnostico, medicamentos (array de strings), dosagens (array de strings), frequencia, duracaoDias e observacoes. " +
            $"Responda APENAS com o objeto JSON, sem texto adicional.\n\n" +
            $"Diagnostico: {diagnostico}. Especie: {especie}. Peso: {pesoKg} kg.";

        var resposta = await EnviarAsync(prompt);

        return ParsearOuPadrao<ProtocoloTratamentoDto>(resposta) ?? new ProtocoloTratamentoDto
        {
            Diagnostico = diagnostico,
            Observacoes = resposta // retorna o texto bruto se o JSON falhar
        };
    }

    /// <inheritdoc/>
    public async Task<string> GerarOrientacoesPostAtendimentoAsync(ConsultaResumoDto consulta)
    {
        var meds = string.Join(", ", consulta.Medicamentos);
        var dieta = consulta.RestricoesDieta is not null
            ? $" Restricoes alimentares: {consulta.RestricoesDieta}."
            : string.Empty;

        var prompt =
            $"Voce e um assistente veterinario. Gere orientacoes pos-atendimento claras para o tutor, em linguagem simples e acessivel.\n\n" +
            $"Especie: {consulta.Especie}. Diagnostico: {consulta.Diagnostico}.\n" +
            $"Medicamentos prescritos: {meds}. Conduta: {consulta.Conduta}.{dieta}";

        return await EnviarAsync(prompt);
    }

    /// <inheritdoc/>
    public async Task<TriagemResultadoDto> RealizarTriagemAsync(SintomasDto sintomas)
    {
        var sintomasLista = string.Join(", ", sintomas.Sintomas);
        var extras = sintomas.IdadeAnos.HasValue ? $" Idade: {sintomas.IdadeAnos} anos." : string.Empty;
        if (sintomas.PesoKg.HasValue) extras += $" Peso: {sintomas.PesoKg} kg.";

        var prompt =
            $"Voce e um assistente veterinario de triagem. Com base nos sintomas descritos, retorne um objeto JSON com os campos: " +
            $"nivelUrgencia (Baixo/Medio/Alto/Emergencia), recomendacao e possiveisCausas (array de strings). " +
            $"Responda APENAS com o objeto JSON, sem texto adicional.\n\n" +
            $"Especie: {sintomas.Especie}. Sintomas: {sintomasLista}.{extras}";

        var resposta = await EnviarAsync(prompt);

        return ParsearOuPadrao<TriagemResultadoDto>(resposta) ?? new TriagemResultadoDto
        {
            NivelUrgencia = "Indeterminado",
            Recomendacao = resposta
        };
    }

    /// <inheritdoc/>
    public async Task<ConsultaEstruturadaDto> EstruturarConsultaAsync(ContextoDaEstruturacaoDto contexto)
    {
        if (string.IsNullOrWhiteSpace(contexto.Transcricao))
            throw new BusinessRuleException("RN-080",
                "Nao ha transcricao para estruturar. A consulta segue pelo prontuario manual.");

        var temPeso = contexto.PesoKg is { } kg && kg > 0;

        var peso = temPeso
            ? $"{contexto.PesoKg} kg"
            : "nao informado";

        // RN-081: dose sem peso e o erro clinico que a regra existe para impedir, e o
        // campo `conduta` e exatamente onde a posologia aparece. O aviso PesoAusente
        // continua indo ao rascunho, mas ele so alerta a interface — nao impede o texto
        // de sair pronto e ser levado tal e qual para a receita. Escrever "Peso: nao
        // informado" no prompt tambem nao impede: o modelo trata como dado faltante e
        // preenche com a dose usual da especie. A guarda tem de ser instrucao explicita.
        var semDose = temPeso
            ? string.Empty
            : "O peso do animal NAO esta cadastrado. NAO sugira dose, posologia nem " +
              "quantidade de medicamento em nenhum campo. No campo conduta, cite o " +
              "medicamento sem dose e registre que a dose depende do peso. ";

        var conhecido = new List<string>();
        if (contexto.Alergias.Count > 0)
            conhecido.Add($"Alergias conhecidas: {string.Join(", ", contexto.Alergias)}.");
        if (contexto.CondicoesPreexistentes.Count > 0)
            conhecido.Add($"Condicoes preexistentes: {string.Join(", ", contexto.CondicoesPreexistentes)}.");

        // Transcricao incompleta precisa ser dita: sem isso o modelo preenche a lacuna
        // por conta propria, e o veterinario nao tem como distinguir o que foi falado
        // do que foi inventado (§7.3).
        var ressalva = contexto.TranscricaoParcial
            ? "ATENCAO: a transcricao esta INCOMPLETA. Nao complete lacunas nem infira o que nao foi dito; " +
              "deixe o campo vazio quando o trecho correspondente nao aparecer na transcricao. "
            : string.Empty;

        // Alerta de seguranca vem primeiro e destacado: e o dado cuja falta pode
        // aparecer numa sugestao de dose (RN-068).
        var alertas = contexto.AlertasAtivos.Count > 0
            ? $"ALERTAS DE SEGURANCA DO ANIMAL: {string.Join("; ", contexto.AlertasAtivos)}. " +
              "Considere-os obrigatoriamente na conduta.\n\n"
            : string.Empty;

        // O relato do Responsavel entra separado da transcricao e rotulado como tal: ele
        // e observacao de leigo, nao achado clinico, e misturar os dois faria a IA tratar
        // "ele parece triste" com o mesmo peso de um exame fisico (RN-005/RN-036).
        var preSintomas = string.IsNullOrWhiteSpace(contexto.PreSintomas)
            ? string.Empty
            : $"Relato do Responsavel no agendamento (observacao de leigo, nao achado clinico):\n" +
              $"{contexto.PreSintomas}\n\n";

        var historico = contexto.HistoricoRelevante.Count > 0
            ? $"Atendimentos anteriores deste animal:\n" +
              string.Join("\n", contexto.HistoricoRelevante.Select(h => $"- {h}")) + "\n\n"
            : string.Empty;

        var prompt =
            $"Voce e um assistente veterinario. Estruture a transcricao de uma consulta em prontuario, " +
            $"usando SOMENTE o que foi dito. Nao invente sintomas, medicamentos, doses nem achados. " +
            $"{ressalva}" +
            $"{semDose}" +
            $"Responda APENAS com um objeto JSON com os campos: anamnese, exameFisico, " +
            $"hipotesesDiagnosticas (array de strings, da mais provavel a menos), conduta e orientacoes. " +
            $"Campo sem informacao na transcricao deve vir como string vazia ou array vazio.\n\n" +
            $"{alertas}" +
            $"Especie: {contexto.Especie}. Raca: {contexto.Raca}. Idade: {contexto.IdadeAnos} anos. " +
            $"Peso: {peso}. {string.Join(" ", conhecido)}\n\n" +
            $"{preSintomas}" +
            $"{historico}" +
            $"Transcricao da consulta:\n{contexto.Transcricao}";

        var resposta = await EnviarAsync(prompt, TokensDaEstruturacao);

        return ParsearOuPadrao<ConsultaEstruturadaDto>(resposta) ?? new ConsultaEstruturadaDto
        {
            // Sem JSON valido nao se inventa estrutura: o texto bruto vai para a
            // anamnese e o veterinario corrige, em vez de receber campos plausiveis
            // que a IA nao produziu de verdade.
            Anamnese = resposta
        };
    }

    // Envia um prompt ao Ollama e retorna a resposta em texto
    /// <summary>
    /// Ponto unico de saida para o LLM — e, por isso, o ponto unico de instrumentacao
    /// da IA.
    /// </summary>
    /// <param name="prompt">Prompt ja montado pelo metodo chamador.</param>
    /// <param name="numPredict">
    /// Teto de tokens da resposta. O padrao serve as sugestoes pontuais; so a
    /// estruturacao do prontuario precisa de mais, e pede explicitamente.
    /// </param>
    /// <param name="operacao">
    /// Preenchido automaticamente pelo compilador com o nome do metodo que chamou
    /// (<see cref="CallerMemberNameAttribute"/>). E o que permite separar, na metrica,
    /// "sugerir diagnostico" de "estruturar consulta" sem obrigar cada chamador a
    /// repetir o proprio nome — e sem correr o risco de alguem esquecer.
    /// </param>
    /// <returns>O texto devolvido pelo modelo.</returns>
    /// <remarks>
    /// O Ollama e a dependencia mais lenta do sistema e a unica cuja latencia varia por
    /// ordens de grandeza conforme o modelo carregado e o hardware. Sem esta medida,
    /// uma consulta que demorou 40 segundos e indistinguivel de uma API lenta; com ela,
    /// o histograma mostra imediatamente que 38 desses segundos foram do modelo.
    /// </remarks>
    private async Task<string> EnviarAsync(
        string prompt, int numPredict = TokensDaResposta, [CallerMemberName] string operacao = "")
    {
        var payload = new OllamaRequest
        {
            Model = _model,
            Prompt = prompt,
            Stream = false,
            Options = new OllamaOptions { Temperature = 0.3f, NumPredict = numPredict }
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Kind.Client: para o backend de tracing, este span e uma chamada de saida — e
        // e assim que o Ollama aparece separado do tempo da propria Vetly.
        using var atividade = VetlyTelemetry.Iniciar($"ia.{operacao}", ActivityKind.Client);
        atividade?.SetTag("vetly.ia.modelo", _model);

        var inicio = Stopwatch.GetTimestamp();
        var resultado = "sucesso";

        try
        {
            var response = await _http.PostAsync("/api/generate", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson, _jsonOptions);
            return result?.Response ?? string.Empty;
        }
        catch (Exception excecao)
        {
            // A falha e medida e re-lancada: quem trata o erro continua sendo o
            // chamador. Instrumentar nao pode mudar o comportamento do codigo.
            resultado = "falha";
            VetlyTelemetry.RegistrarFalha(atividade, excecao);
            throw;
        }
        finally
        {
            // No finally: um timeout do LLM e justamente o caso em que a duracao mais
            // interessa, e ele chega aqui como excecao.
            VetlyTelemetry.DuracaoDaIa.Record(
                Stopwatch.GetElapsedTime(inicio).TotalMilliseconds,
                new KeyValuePair<string, object?>("operacao", operacao),
                new KeyValuePair<string, object?>("resultado", resultado));
        }
    }

    private static T? ParsearOuPadrao<T>(string json) where T : class
    {
        try
        {
            // extrai apenas o bloco JSON se houver texto ao redor
            var inicio = json.IndexOf('{');
            var fim = json.LastIndexOf('}');
            if (inicio < 0 || fim < 0) return null;
            return JsonSerializer.Deserialize<T>(json[inicio..(fim + 1)], _jsonOptions);
        }
        catch { return null; }
    }

    private static List<T> ParsearListaOuVazio<T>(string json)
    {
        try
        {
            var inicio = json.IndexOf('[');
            var fim = json.LastIndexOf(']');
            if (inicio < 0 || fim < 0) return [];
            return JsonSerializer.Deserialize<List<T>>(json[inicio..(fim + 1)], _jsonOptions) ?? [];
        }
        catch { return []; }
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // Payload interno para a API do Ollama
    private sealed class OllamaRequest
    {
        [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
        [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
        [JsonPropertyName("stream")] public bool Stream { get; set; }
        [JsonPropertyName("options")] public OllamaOptions Options { get; set; } = new();
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("temperature")] public float Temperature { get; set; }
        [JsonPropertyName("num_predict")] public int NumPredict { get; set; }
    }

    private sealed class OllamaResponse
    {
        [JsonPropertyName("response")] public string Response { get; set; } = string.Empty;
    }
}
