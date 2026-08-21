using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Vetly.API.HealthChecks;

public static class  HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true
    }; // Configura a serialização JSON para ser indentada, facilitando a leitura.

    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
		//Isso avisa ao navegador ou cliente HTTP que a resposta retornada é um JSON codificado em UTF-8.
		var payload = new
        {
            status = report.Status.ToString(),
            duracaoTotalMs = report.TotalDuration.TotalMilliseconds,
            checkes = report.Entries.Select(e => new
            {
                nome = e.Key,
                status = e.Value.Status.ToString(),
                descricao = e.Value.Description,
                duracaoMs = e.Value.Duration.TotalMilliseconds,
                tags = e.Value.Tags
            }) 
        }; //Ele monta a estrutura do JSON que será enviado na resposta
		return context.Response.WriteAsync(JsonSerializer.Serialize(payload, _options));
        //Pega o objeto customizado(payload) que criamos com os dados do healthcheck e transformamos em uma string em formato Json
    }
}