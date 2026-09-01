using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// <b>Fixture</b> de infraestrutura dos testes de integração: sobe a API inteira em
/// memória — pipeline, filtros, autenticação, model binding, serialização e worker —
/// trocando apenas o Oracle por um banco InMemory.
/// </summary>
/// <remarks>
/// <para>
/// A troca é cirúrgica de propósito. Testar contra o Oracle real transformaria a suíte
/// em algo que só roda com credencial, VPN e banco de pé; substituir o serviço de
/// aplicação por um dublê, no outro extremo, testaria o dublê. O meio-termo correto é
/// manter <b>todo</b> o resto real e trocar só a borda de persistência — é assim que
/// estes testes pegam o que os de unidade não pegam: o filtro que barra a rota antes do
/// serviço, o DTO que não desserializa, a policy que exige uma role que ninguém emite.
/// </para>
/// <para>
/// <c>UseInternalServiceProvider</c> merece a explicação que parece detalhe e não é: o
/// EF Core resolve os serviços internos dele a partir do container da aplicação, e o
/// container da Vetly tem o provider Oracle registrado. Sem um provider interno próprio
/// e isolado, o EF encontraria os dois providers e recusaria a configuração.
/// </para>
/// </remarks>
public class VetlyWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Nome do banco InMemory. Estático de propósito: o seeding feito por um teste
    /// precisa ser visível para os demais, e o nome é o que amarra todos ao mesmo banco.
    /// </summary>
    public static readonly string DatabaseName = "VetlyIntegrationTest_" + Guid.NewGuid();

    /// <summary>
    /// ServiceProvider isolado, com apenas o provider InMemory registrado — ver a nota
    /// sobre <c>UseInternalServiceProvider</c> na documentação da classe.
    /// </summary>
    private static readonly IServiceProvider InMemoryServiceProvider =
        new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o DbContextOptions<VetlyDbContext> configurado com Oracle
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<VetlyDbContext>));

            if (descriptor != null)
                services.Remove(descriptor);

            // Substitui por InMemory usando o service provider isolado
            services.AddDbContext<VetlyDbContext>(options =>
                options
                    .UseInternalServiceProvider(InMemoryServiceProvider)
                    .UseInMemoryDatabase(DatabaseName));
        });
    }
}
