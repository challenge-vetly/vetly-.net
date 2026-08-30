using Microsoft.Extensions.Logging.Abstractions;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Infrastructure.Adapters;

namespace Vetly.UnitTests;

/// <summary>
/// Testes do adaptador simulado de validacao de CRMV (RN-107, camada C2).
/// O contrato exige as quatro respostas — inclusive Indisponivel, que e o que
/// impede a plataforma de aprovar um registro por omissao.
/// </summary>
public class CrmvAdapterSimuladoTests
{
    private static CrmvAdapterSimulado CriarAdapter() =>
        new(NullLogger<CrmvAdapterSimulado>.Instance);

    [Theory]
    [InlineData("12345-SP", ResultadoValidacaoCrmv.Valido)]
    [InlineData("12347-SP", ResultadoValidacaoCrmv.Indisponivel)]
    [InlineData("12348-SP", ResultadoValidacaoCrmv.Invalido)]
    [InlineData("12349-SP", ResultadoValidacaoCrmv.Suspenso)]
    public async Task ValidarRegistroAsync_EDeterministicoPeloUltimoDigito(
        string crmv, ResultadoValidacaoCrmv esperado)
    {
        var resultado = await CriarAdapter().ValidarRegistroAsync(crmv, "SP");

        Assert.Equal(esperado, resultado.Resultado);
        Assert.NotEqual(default, resultado.ConsultadoEm);
    }

    [Fact]
    public async Task ValidarRegistroAsync_UfDivergenteDoRegistro_RetornaInvalido()
    {
        // Um CRMV pertence ao conselho de um estado: registro de SP com atuacao em RJ nao confere
        var resultado = await CriarAdapter().ValidarRegistroAsync("12345-SP", "RJ");

        Assert.Equal(ResultadoValidacaoCrmv.Invalido, resultado.Resultado);
    }

    [Fact]
    public async Task ValidarRegistroAsync_NuncaLancaPorIndisponibilidade()
    {
        // O contrato do vetly-tech §7.5 e explicito: indisponibilidade e resposta, nao excecao
        var resultado = await CriarAdapter().ValidarRegistroAsync("12347-SP", "SP");

        Assert.Equal(ResultadoValidacaoCrmv.Indisponivel, resultado.Resultado);
        Assert.NotNull(resultado.Mensagem);
    }
}
