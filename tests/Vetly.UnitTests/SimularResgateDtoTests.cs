using System.ComponentModel.DataAnnotations;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Pedido de resgate nas duas formas (Sprint 4): só <c>itemId</c>, a atual, e os
/// campos soltos da forma legada, que continuam aceitos para o app publicado.
/// </summary>
public class SimularResgateDtoTests
{
    private static List<ValidationResult> Validar(SimularResgateDto dto)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
        return resultados;
    }

    [Fact]
    public void SoItemId_EValido()
    {
        // Arrange — exatamente o que o app novo manda
        var dto = new SimularResgateDto { ItemId = Guid.NewGuid() };

        // Act
        var erros = Validar(dto);

        // Assert
        Assert.Empty(erros);
    }

    [Fact]
    public void ItemIdVazio_EInvalido()
    {
        // Arrange
        var dto = new SimularResgateDto { ItemId = Guid.Empty };

        // Act
        var erros = Validar(dto);

        // Assert
        Assert.Contains(erros, e => e.MemberNames.Contains(nameof(SimularResgateDto.ItemId)));
    }

    [Fact]
    public void FormaLegadaCompleta_EValida()
    {
        // Arrange
        var dto = new SimularResgateDto
        {
            ItemRef = "racao", Categoria = CategoriaItem.Alimentacao, Pontos = 100
        };

        // Act
        var erros = Validar(dto);

        // Assert
        Assert.Empty(erros);
    }

    [Fact]
    public void FormaLegadaIncompleta_ApontaCadaCampoQueFalta()
    {
        // Arrange — sem itemId, sem itemRef, categoria 0 e pontos 0
        var dto = new SimularResgateDto();

        // Act
        var campos = Validar(dto).SelectMany(e => e.MemberNames).ToList();

        // Assert
        Assert.Contains(nameof(SimularResgateDto.ItemRef), campos);
        Assert.Contains(nameof(SimularResgateDto.Categoria), campos);
        Assert.Contains(nameof(SimularResgateDto.Pontos), campos);
    }
}
