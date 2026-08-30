using System.Reflection;
using Moq;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Exceptions;
using Vetly.Application.Factories;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do DocumentoService.
/// Verifica que a factory correta e selecionada via IEnumerable e que RN-082 e aplicada.
/// </summary>
public class DocumentoServiceTests
{
    private readonly Mock<IDocumentoRepository> _docRepoMock = new();
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();

    private DocumentoService CriarServico(params IDocumentoFactory[] factories) =>
        new(_docRepoMock.Object, _consultaRepoMock.Object,
            _vetRepoMock.Object, _animalRepoMock.Object, factories);

    private static Consulta CriarConsultaValidada()
    {
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1),
            ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.ConfirmarPagamento();
        consulta.ValidarDiagnostico();
        return consulta;
    }

    [Fact]
    public async Task Gerar_SelecionaFactoryCorreta_PorTipoDocumento()
    {
        // Arrange — duas factories registradas; deve escolher a de Prontuario
        var factoryProntuario = new Mock<IDocumentoFactory>();
        factoryProntuario.Setup(f => f.TipoSuportado).Returns(TipoDocumento.Prontuario);
        factoryProntuario
            .Setup(f => f.Criar(It.IsAny<ConsultaDto>(), It.IsAny<VeterinarioDto>(), It.IsAny<AnimalDto>()))
            .Returns(new Documento(TipoDocumento.Prontuario, "12345-SP", Guid.NewGuid()));

        var factoryReceita = new Mock<IDocumentoFactory>();
        factoryReceita.Setup(f => f.TipoSuportado).Returns(TipoDocumento.ReceitaVeterinaria);

        var consulta = CriarConsultaValidada();
        var consultaId = consulta.Id;

        _consultaRepoMock
            .Setup(r => r.ObterPorIdAsync(consultaId))
            .ReturnsAsync(consulta);

        var crmv = new Crmv("12345-SP");
        var vet = new Veterinario("Dr. Vet", crmv, "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync(vet);

        var animal = new Animal("Rex", "Canino", "Labrador", new DateTime(2020, 1, 1), Guid.NewGuid());
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync(animal);

        _docRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Documento>())).Returns(Task.CompletedTask);
        _docRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var service = CriarServico(factoryProntuario.Object, factoryReceita.Object);

        // Act
        var resultado = await service.GerarAsync(consultaId, TipoDocumento.Prontuario);

        // Assert — a factory de Prontuario foi chamada; a de Receita nao
        factoryProntuario.Verify(f => f.Criar(It.IsAny<ConsultaDto>(), It.IsAny<VeterinarioDto>(), It.IsAny<AnimalDto>()), Times.Once);
        factoryReceita.Verify(f => f.Criar(It.IsAny<ConsultaDto>(), It.IsAny<VeterinarioDto>(), It.IsAny<AnimalDto>()), Times.Never);
        Assert.Equal(TipoDocumento.Prontuario, resultado.TipoDocumento);
    }

    [Fact]
    public async Task Gerar_LancaBusinessRuleException_QuandoDiagnosticoNaoValidado()
    {
        // Consulta sem diagnostico validado
        var consulta = new Consulta(
            DateTime.UtcNow.AddDays(1),
            ModalidadeAtendimento.Presencial,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        consulta.ConfirmarPagamento();
        // NÃO chama ValidarDiagnostico()

        _consultaRepoMock
            .Setup(r => r.ObterPorIdAsync(consulta.Id))
            .ReturnsAsync(consulta);

        var service = CriarServico();

        // Act + Assert
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.GerarAsync(consulta.Id, TipoDocumento.Prontuario));

        Assert.Equal("RN-082", ex.Codigo);
    }

    // ── Helper para controlar DataGeracao nos testes de correção ─────────────

    private static Documento CriarDocumentoComDataGeracao(DateTime dataGeracao)
    {
        var doc = new Documento(TipoDocumento.Prontuario, "12345-SP", Guid.NewGuid());
        typeof(Documento)
            .GetField("<DataGeracao>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(doc, dataGeracao);
        return doc;
    }

    [Fact]
    public async Task CorrigirAsync_DentroDe24h_SemJustificativa_Sucesso()
    {
        var doc = CriarDocumentoComDataGeracao(DateTime.UtcNow.AddHours(-12));

        _docRepoMock.Setup(r => r.ObterPorIdAsync(doc.Id)).ReturnsAsync(doc);
        _docRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Documento>())).Returns(Task.CompletedTask);
        _docRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().CorrigirAsync(doc.Id, "novos dados", null, "12345-SP");

        Assert.NotNull(resultado);
        Assert.Equal(doc.Id, resultado.VersaoOriginalId);
        Assert.NotNull(resultado.DataCorrecao);
    }

    [Fact]
    public async Task CorrigirAsync_Apos24h_SemJustificativa_LancaBusinessRuleExceptionRN034()
    {
        var doc = CriarDocumentoComDataGeracao(DateTime.UtcNow.AddHours(-25));

        _docRepoMock.Setup(r => r.ObterPorIdAsync(doc.Id)).ReturnsAsync(doc);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().CorrigirAsync(doc.Id, "novos dados", null, "12345-SP"));

        Assert.Equal("RN-089", ex.Codigo);
    }

    [Fact]
    public async Task CorrigirAsync_Apos24h_ComJustificativa_Sucesso()
    {
        var doc = CriarDocumentoComDataGeracao(DateTime.UtcNow.AddHours(-25));

        _docRepoMock.Setup(r => r.ObterPorIdAsync(doc.Id)).ReturnsAsync(doc);
        _docRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Documento>())).Returns(Task.CompletedTask);
        _docRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().CorrigirAsync(doc.Id, "novos dados", "Erro de preenchimento", "12345-SP");

        Assert.Equal(doc.Id, resultado.VersaoOriginalId);
        Assert.Equal("12345-SP", resultado.CrmvSolicitanteCorrecao);
    }

    [Fact]
    public async Task Gerar_LancaInvalidOperationException_QuandoTipoNaoRegistrado()
    {
        var consulta = CriarConsultaValidada();

        _consultaRepoMock
            .Setup(r => r.ObterPorIdAsync(consulta.Id))
            .ReturnsAsync(consulta);

        var crmv = new Crmv("12345-SP");
        var vet = new Veterinario("Dr. Vet", crmv, "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync(vet);

        var animal = new Animal("Rex", "Canino", "Labrador", new DateTime(2020, 1, 1), Guid.NewGuid());
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync(animal);

        // Nenhuma factory registrada
        var service = CriarServico();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GerarAsync(consulta.Id, TipoDocumento.Atestado));
    }
}
