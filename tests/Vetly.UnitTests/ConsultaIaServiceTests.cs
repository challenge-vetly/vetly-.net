using Microsoft.Extensions.Configuration;
using Moq;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do ConsultaIaService.
/// Cobre IA-001 (peso obrigatorio antes de chamar o modelo), o fluxo de decisao
/// (Aprovar/Corrigir/NaoAprovar) e a trilha de auditoria.
/// </summary>
public class ConsultaIaServiceTests
{
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<ILogAuditoriaIARepository> _logRepoMock = new();
    private readonly Mock<IOllamaService> _ollamaMock = new();

    private ConsultaIaService CriarServico() => new(
        _consultaRepoMock.Object, _animalRepoMock.Object, _vetRepoMock.Object,
        _logRepoMock.Object, _ollamaMock.Object, TimeProvider.System,
        new ConfigurationBuilder().Build());

    private static Consulta CriarConsulta(Guid vetId, Guid animalId)
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1), ModalidadeAtendimento.Presencial, TipoServico.Consulta,
            vetId, animalId, Guid.NewGuid());
        return consulta;
    }

    private static Veterinario CriarVet() =>
        new("Dr. Vet", new Crmv("12345-SP"), "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

    [Fact]
    public async Task SugerirProtocoloAsync_AnimalSemPeso_LancaBusinessRuleExceptionIA001_SemChamarOModelo()
    {
        var vet = CriarVet();
        var animal = new Animal("Rex", "Canino", "Labrador", SexoAnimal.Macho, new DateTime(2020, 1, 1), Guid.NewGuid());
        var consulta = CriarConsulta(vet.Id, animal.Id);

        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().SugerirProtocoloAsync(consulta.Id));

        Assert.Equal("IA-001", ex.Codigo);
        _ollamaMock.Verify(
            o => o.SugerirProtocoloAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>()),
            Times.Never);
    }

    [Fact]
    public async Task SugerirDiagnosticoAsync_ContextoValido_GravaLogPendenteERetornaLogId()
    {
        var vet = CriarVet();
        var animal = new Animal("Rex", "Canino", "Labrador", SexoAnimal.Macho, new DateTime(2020, 1, 1), Guid.NewGuid(), pesoKg: 20m);
        var consulta = CriarConsulta(vet.Id, animal.Id);

        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _ollamaMock.Setup(o => o.SugerirDiagnosticoAsync(It.IsAny<ContextoClinicoDto>()))
            .ReturnsAsync([new HipoteseDiagnosticaDto { Hipotese = "Gastrite", NivelConfianca = "medio" }]);
        _logRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<LogAuditoriaIA>())).Returns(Task.CompletedTask);
        _logRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().SugerirDiagnosticoAsync(consulta.Id);

        Assert.Single(resultado.Hipoteses);
        Assert.NotEqual(Guid.Empty, resultado.LogId);
        _logRepoMock.Verify(r => r.AdicionarAsync(It.Is<LogAuditoriaIA>(l => l.Pendente)), Times.Once);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_Corrigir_SemConteudoCorrigido_LancaBusinessRuleExceptionIA003()
    {
        var consulta = CriarConsulta(Guid.NewGuid(), Guid.NewGuid());
        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);

        var dto = new RegistrarDecisaoIADto { Tipo = TipoSugestaoIA.Diagnostico, Decisao = DecisaoVeterinario.Corrigir };

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().RegistrarDecisaoAsync(consulta.Id, dto));

        Assert.Equal("IA-003", ex.Codigo);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_Aprovar_DefineConteudoFinalIgualAoSugerido()
    {
        var consulta = CriarConsulta(Guid.NewGuid(), Guid.NewGuid());
        var log = new LogAuditoriaIA(consulta.Id, consulta.VeterinarioId, "12345-SP", "llama3.1",
            TipoSugestaoIA.Diagnostico, "Gastrite (sugerido)", DateTime.UtcNow);

        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _logRepoMock.Setup(r => r.ObterPendenteAsync(consulta.Id, TipoSugestaoIA.Diagnostico)).ReturnsAsync(log);
        _logRepoMock.Setup(r => r.Atualizar(It.IsAny<LogAuditoriaIA>()));
        _logRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _consultaRepoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _consultaRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = new RegistrarDecisaoIADto { Tipo = TipoSugestaoIA.Diagnostico, Decisao = DecisaoVeterinario.Aprovar };
        var resultado = await CriarServico().RegistrarDecisaoAsync(consulta.Id, dto);

        Assert.True(resultado.EstadoFinalDefinido);
        Assert.Equal("Gastrite (sugerido)", consulta.DiagnosticoFinal);
        Assert.Equal("Gastrite (sugerido)", log.ConteudoFinal);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_Corrigir_ConteudoFinalDivergeDoSugerido_IaNaoReinfere()
    {
        var consulta = CriarConsulta(Guid.NewGuid(), Guid.NewGuid());
        var log = new LogAuditoriaIA(consulta.Id, consulta.VeterinarioId, "12345-SP", "llama3.1",
            TipoSugestaoIA.Diagnostico, "Gastrite (sugerido pela IA)", DateTime.UtcNow);

        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _logRepoMock.Setup(r => r.ObterPendenteAsync(consulta.Id, TipoSugestaoIA.Diagnostico)).ReturnsAsync(log);
        _logRepoMock.Setup(r => r.Atualizar(It.IsAny<LogAuditoriaIA>()));
        _logRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _consultaRepoMock.Setup(r => r.Atualizar(It.IsAny<Consulta>()));
        _consultaRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = new RegistrarDecisaoIADto
        {
            Tipo = TipoSugestaoIA.Diagnostico, Decisao = DecisaoVeterinario.Corrigir,
            ConteudoCorrigido = "Insuficiencia renal cronica (texto do vet)"
        };
        await CriarServico().RegistrarDecisaoAsync(consulta.Id, dto);

        Assert.Equal("Insuficiencia renal cronica (texto do vet)", consulta.DiagnosticoFinal);
        Assert.NotEqual(log.ConteudoSugerido, log.ConteudoFinal);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_NaoAprovar_NaoDefineEstadoFinal()
    {
        var consulta = CriarConsulta(Guid.NewGuid(), Guid.NewGuid());
        var log = new LogAuditoriaIA(consulta.Id, consulta.VeterinarioId, "12345-SP", "llama3.1",
            TipoSugestaoIA.Diagnostico, "Gastrite (sugerido)", DateTime.UtcNow);

        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _logRepoMock.Setup(r => r.ObterPendenteAsync(consulta.Id, TipoSugestaoIA.Diagnostico)).ReturnsAsync(log);
        _logRepoMock.Setup(r => r.Atualizar(It.IsAny<LogAuditoriaIA>()));
        _logRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = new RegistrarDecisaoIADto { Tipo = TipoSugestaoIA.Diagnostico, Decisao = DecisaoVeterinario.NaoAprovar };
        var resultado = await CriarServico().RegistrarDecisaoAsync(consulta.Id, dto);

        Assert.False(resultado.EstadoFinalDefinido);
        Assert.Null(consulta.DiagnosticoFinal);
        _consultaRepoMock.Verify(r => r.SalvarAsync(), Times.Never);
    }

    [Fact]
    public async Task RegistrarDecisaoAsync_SemSugestaoPendente_LancaNotFoundException()
    {
        var consulta = CriarConsulta(Guid.NewGuid(), Guid.NewGuid());
        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);
        _logRepoMock.Setup(r => r.ObterPendenteAsync(consulta.Id, TipoSugestaoIA.Protocolo))
            .ReturnsAsync((LogAuditoriaIA?)null);

        var dto = new RegistrarDecisaoIADto { Tipo = TipoSugestaoIA.Protocolo, Decisao = DecisaoVeterinario.Aprovar };

        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().RegistrarDecisaoAsync(consulta.Id, dto));
    }
}
