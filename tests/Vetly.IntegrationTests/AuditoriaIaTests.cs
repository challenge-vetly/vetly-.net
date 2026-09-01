using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;
using Vetly.Infrastructure.Repositories;

namespace Vetly.IntegrationTests;

/// <summary>
/// Trilha de auditoria das decisoes sobre conteudo de IA (RN-082).
///
/// Fica neste projeto porque o repositorio append-only vive na Infrastructure.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class AuditoriaIaTests
{
    private readonly HttpClient _client;

    public AuditoriaIaTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static VetlyDbContext CriarContexto(string nome) =>
        new(new DbContextOptionsBuilder<VetlyDbContext>().UseInMemoryDatabase(nome).Options);

    private static LogAuditoriaIa Registro(
        Guid consultaId, DecisaoSobreRascunho decisao, string conteudo = "{}") =>
        new(consultaId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            decisao, conteudo, null, decisao != DecisaoSobreRascunho.Aprovado, "ollama/llama3.1");

    [Fact]
    public async Task Trilha_AcumulaAsDecisoesDaConsultaDaMaisRecenteParaAMaisAntiga()
    {
        using var contexto = CriarContexto($"auditoria-{Guid.NewGuid():N}");
        var repo = new AuditoriaIaRepository(contexto);

        var consultaId = Guid.NewGuid();

        var recusa = Registro(consultaId, DecisaoSobreRascunho.NaoAprovado, string.Empty);
        await repo.AdicionarAsync(recusa);
        await repo.SalvarAsync();

        var manual = Registro(consultaId, DecisaoSobreRascunho.Manual);
        await repo.AdicionarAsync(manual);
        await repo.SalvarAsync();

        // Ruido de outra consulta nao pode aparecer nesta trilha
        await repo.AdicionarAsync(Registro(Guid.NewGuid(), DecisaoSobreRascunho.Aprovado));
        await repo.SalvarAsync();

        var trilha = (await repo.ObterDaConsultaAsync(consultaId)).ToList();

        Assert.Equal(2, trilha.Count);
        Assert.All(trilha, l => Assert.Equal(consultaId, l.ConsultaId));
        Assert.True(trilha[0].RegistradoEm >= trilha[1].RegistradoEm);
    }

    [Fact]
    public async Task Trilha_NaoOfereceComoAlterarNemRemoverUmRegistro()
    {
        var contrato = typeof(Vetly.Application.Interfaces.IAuditoriaIaRepository);

        var metodos = contrato.GetMethods().Select(m => m.Name).ToList();

        // O contrato e append-only de proposito: um registro que pode ser reescrito
        // depois nao prova que houve decisao humana (RN-082)
        Assert.DoesNotContain(metodos, m => m.StartsWith("Atualizar") || m.StartsWith("Remover"));

        // A entidade tambem nao expoe mutacao depois de gravada
        var mutadores = typeof(LogAuditoriaIa).GetMethods()
            .Where(m => m.DeclaringType == typeof(LogAuditoriaIa) && m.IsPublic && !m.IsSpecialName)
            .Select(m => m.Name);

        Assert.Empty(mutadores);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Decisao_SemToken_NaoEAlcancavel()
    {
        var id = Guid.NewGuid();

        var corpo = new StringContent("""{"decisao":"Aprovado"}""", Encoding.UTF8, "application/json");

        var decidir = await _client.PutAsync($"/api/consultas/{id}/validar-diagnostico", corpo);
        var manual = await _client.PostAsync($"/api/consultas/{id}/prontuario-manual",
            new StringContent("""{"conteudo":{"anamnese":"x"}}""", Encoding.UTF8, "application/json"));
        var trilha = await _client.GetAsync($"/api/consultas/{id}/auditoria-ia");

        Assert.Equal(HttpStatusCode.Unauthorized, decidir.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, manual.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, trilha.StatusCode);
    }
}
