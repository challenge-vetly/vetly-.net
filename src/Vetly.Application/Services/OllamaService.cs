using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de integracao com o Ollama (LLM local).
/// Todos os retornos sao sugestoes de IA — o veterinario deve validar antes de qualquer acao clinica (RN-082).
/// </summary>
public class OllamaService : IOllamaService
{
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

        var peso = contexto.PesoKg is { } kg && kg > 0
            ? $"{kg} kg"
            : "nao informado";

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

        var prompt =
            $"Voce e um assistente veterinario. Estruture a transcricao de uma consulta em prontuario, " +
            $"usando SOMENTE o que foi dito. Nao invente sintomas, medicamentos, doses nem achados. " +
            $"{ressalva}" +
            $"Responda APENAS com um objeto JSON com os campos: anamnese, exameFisico, " +
            $"hipotesesDiagnosticas (array de strings, da mais provavel a menos), conduta e orientacoes. " +
            $"Campo sem informacao na transcricao deve vir como string vazia ou array vazio.\n\n" +
            $"Especie: {contexto.Especie}. Raca: {contexto.Raca}. Idade: {contexto.IdadeAnos} anos. " +
            $"Peso: {peso}. {string.Join(" ", conhecido)}\n\n" +
            $"Transcricao da consulta:\n{contexto.Transcricao}";

        var resposta = await EnviarAsync(prompt);

        return ParsearOuPadrao<ConsultaEstruturadaDto>(resposta) ?? new ConsultaEstruturadaDto
        {
            // Sem JSON valido nao se inventa estrutura: o texto bruto vai para a
            // anamnese e o veterinario corrige, em vez de receber campos plausiveis
            // que a IA nao produziu de verdade.
            Anamnese = resposta
        };
    }

    // Envia um prompt ao Ollama e retorna a resposta em texto
    private async Task<string> EnviarAsync(string prompt)
    {
        var payload = new OllamaRequest
        {
            Model = _model,
            Prompt = prompt,
            Stream = false,
            Options = new OllamaOptions { Temperature = 0.3f, NumPredict = 500 }
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/generate", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OllamaResponse>(responseJson, _jsonOptions);
        return result?.Response ?? string.Empty;
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
