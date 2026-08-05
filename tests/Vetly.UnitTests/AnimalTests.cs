using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.Exceptions;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios de dominio puro para Animal.
/// Cobre validacao de peso (RN-096.2) e a trava de ocultacao de alertas de seguranca (RN-088).
/// </summary>
public class AnimalTests
{
    private static Animal CriarAnimal() =>
        new("Rex", "Canino", "Labrador", SexoAnimal.Macho, new DateTime(2020, 1, 1), Guid.NewGuid());

    [Fact]
    public void Ctor_PesoZeroOuNegativo_LancaDomainExceptionANIMAL001()
    {
        var ex = Assert.Throws<DomainException>(() =>
            new Animal("Rex", "Canino", "Labrador", SexoAnimal.Macho, new DateTime(2020, 1, 1), Guid.NewGuid(), pesoKg: 0m));

        Assert.Equal("ANIMAL-001", ex.Codigo);
    }

    [Fact]
    public void AtualizarPeso_PesoNegativo_LancaDomainExceptionANIMAL001()
    {
        var animal = CriarAnimal();

        var ex = Assert.Throws<DomainException>(() => animal.AtualizarPeso(-1m));

        Assert.Equal("ANIMAL-001", ex.Codigo);
    }

    [Fact]
    public void AtualizarPeso_PesoValido_AtualizaPesoKg()
    {
        var animal = CriarAnimal();

        animal.AtualizarPeso(12.4m);

        Assert.Equal(12.4m, animal.PesoKg);
    }

    [Fact]
    public void OcultarRegistro_ProntuarioEhAlertaSeguranca_LancaDomainExceptionANIMAL002()
    {
        var animal = CriarAnimal();

        var ex = Assert.Throws<DomainException>(
            () => animal.OcultarRegistro(Guid.NewGuid(), prontuarioEhAlertaSeguranca: true, DateTime.UtcNow));

        Assert.Equal("ANIMAL-002", ex.Codigo);
    }

    [Fact]
    public void OcultarRegistro_ProntuarioComum_CriaRegistroOcultado()
    {
        var animal = CriarAnimal();
        var prontuarioId = Guid.NewGuid();
        var agora = DateTime.UtcNow;

        var registro = animal.OcultarRegistro(prontuarioId, prontuarioEhAlertaSeguranca: false, agora);

        Assert.Equal(animal.Id, registro.AnimalId);
        Assert.Equal(prontuarioId, registro.ProntuarioId);
        Assert.Equal(agora, registro.DataOcultacao);
    }
}
