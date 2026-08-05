using Moq;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do VeterinarioService.
/// Cobre RN-011 (validação de CRMV) e RN-008 (retorno de agendamentos futuros no soft delete).
/// </summary>
public class VeterinarioServiceTests
{
    private readonly Mock<IVeterinarioRepository> _repoMock = new();

    private VeterinarioService CriarServico() => new(_repoMock.Object);

    private static CriarVeterinarioDto CriarDto(string crmv = "12345-SP") => new()
    {
        Nome = "Dr. Teste",
        Crmv = crmv,
        UfAtuacao = "SP",
        Persona = PersonaVeterinario.Autonomo,
        Plano = PlanoAssinatura.Profissional
    };

    [Fact]
    public async Task CriarAsync_CrmvValido_RetornaVeterinarioDtoComIdPreenchido()
    {
        _repoMock.Setup(r => r.ObterPorCrmvAsync("12345-SP")).ReturnsAsync((Veterinario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Veterinario>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().CriarAsync(CriarDto("12345-SP"));

        Assert.NotEqual(Guid.Empty, resultado.Id);
        Assert.Equal("12345-SP", resultado.Crmv);
    }

    [Fact]
    public async Task CriarAsync_CrmvInvalido_LancaValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().CriarAsync(CriarDto("ABC-SP")));
    }

    [Fact]
    public async Task CriarAsync_CrmvDuplicado_LancaBusinessRuleExceptionRN011()
    {
        var crmv = new Crmv("12345-SP");
        var vetExistente = new Veterinario("Dr. Existente", crmv, "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        _repoMock.Setup(r => r.ObterPorCrmvAsync("12345-SP")).ReturnsAsync(vetExistente);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().CriarAsync(CriarDto("12345-SP")));

        Assert.Equal("RN-011", ex.Codigo);
    }

    [Fact]
    public async Task DesativarAsync_ComConsultaFutura_RetornaAgendamentoEDesativaVet()
    {
        var crmv = new Crmv("12345-SP");
        var vet = new Veterinario("Dr. Vet", crmv, "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        var consultaFutura = new Consulta(
            DateTime.UtcNow.AddDays(3), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            vet.Id, Guid.NewGuid(), Guid.NewGuid());

        _repoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.ObterAgendaFuturaAsync(vet.Id))
            .ReturnsAsync(new List<Consulta> { consultaFutura });
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Veterinario>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var agendamentos = (await CriarServico().DesativarAsync(vet.Id)).ToList();

        Assert.Single(agendamentos);
        Assert.False(vet.Ativo);
    }
}
