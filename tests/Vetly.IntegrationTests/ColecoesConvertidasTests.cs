using Microsoft.EntityFrameworkCore;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// Regressao: as colecoes persistidas por value converter (alertas, especialidades,
/// especies atendidas) precisam de ValueComparer. Sem ele o EF Core compara a colecao
/// por REFERENCIA — os metodos de dominio mutam a mesma lista, o snapshot aponta para
/// ela, a mudanca nao e detectada e o UPDATE nunca acontece.
/// </summary>
public class ColecoesConvertidasTests
{
    private static VetlyDbContext CriarContexto(string nome) =>
        new(new DbContextOptionsBuilder<VetlyDbContext>()
            .UseInMemoryDatabase(nome)
            .Options);

    [Fact]
    public async Task AdicionarAlerta_EmAnimalJaPersistido_ChegaAoBanco()
    {
        var nomeDoBanco = $"colecoes_animal_{Guid.NewGuid()}";
        Guid animalId;

        await using (var ctx = CriarContexto(nomeDoBanco))
        {
            var animal = new Animal("Thor", "Canino", "SRD", DateTime.UtcNow.AddYears(-3), Guid.NewGuid());
            ctx.Animais.Add(animal);
            await ctx.SaveChangesAsync();
            animalId = animal.Id;
        }

        await using (var ctx = CriarContexto(nomeDoBanco))
        {
            var animal = await ctx.Animais.FirstAsync(a => a.Id == animalId);

            // Mutacao in-place: exatamente o caso que a ausencia de comparer engolia
            animal.AdicionarAlerta("Alergia a dipirona");
            await ctx.SaveChangesAsync();
        }

        await using (var verificacao = CriarContexto(nomeDoBanco))
        {
            var animal = await verificacao.Animais.FirstAsync(a => a.Id == animalId);
            Assert.Contains("Alergia a dipirona", animal.AlertasAtivos);
        }
    }

    [Fact]
    public async Task AdicionarEspecialidade_EmVeterinarioJaPersistido_ChegaAoBanco()
    {
        var nomeDoBanco = $"colecoes_vet_{Guid.NewGuid()}";
        Guid vetId;

        await using (var ctx = CriarContexto(nomeDoBanco))
        {
            var vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
                PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
            ctx.Veterinarios.Add(vet);
            await ctx.SaveChangesAsync();
            vetId = vet.Id;
        }

        await using (var ctx = CriarContexto(nomeDoBanco))
        {
            var vet = await ctx.Veterinarios.FirstAsync(v => v.Id == vetId);
            vet.AdicionarEspecialidade("Ortopedia");
            vet.AdicionarEspecie("Canino");
            await ctx.SaveChangesAsync();
        }

        await using (var verificacao = CriarContexto(nomeDoBanco))
        {
            var vet = await verificacao.Veterinarios.FirstAsync(v => v.Id == vetId);
            Assert.Contains("Ortopedia", vet.Especialidades);
            Assert.Contains("Canino", vet.EspeciesAtendidas);
        }
    }
}
