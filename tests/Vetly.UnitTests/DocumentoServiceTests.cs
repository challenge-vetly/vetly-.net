using System.Reflection;
using System.Text.Json;
using Moq;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.DTOs.Documento;
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
/// Verifica que a factory correta e selecionada via IEnumerable, que RN-082 e aplicada
/// e que o conteudo sai do estado final aprovado pelo veterinario (RN-083).
/// </summary>
public class DocumentoServiceTests
{
    private readonly Mock<IDocumentoRepository> _docRepoMock = new();
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();
    private readonly Mock<IAnimalRepository> _animalRepoMock = new();
    private readonly Mock<ITutorRepository> _tutorRepoMock = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepoMock = new();
    private readonly Mock<IAuditoriaIaRepository> _auditoriaMock = new();
    private readonly Mock<IMidiaRepository> _midiaRepoMock = new();
    private readonly Mock<IStorageAdapter> _storageMock = new();
    private readonly Mock<IGeradorDePdf> _pdfMock = new();
    private readonly Mock<IAssinaturaAdapter> _assinaturaMock = new();
    private readonly Mock<IUsuarioAtual> _usuarioMock = new();

    public DocumentoServiceTests()
    {
        _docRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Documento>())).Returns(Task.CompletedTask);
        _docRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _midiaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Midia>())).Returns(Task.CompletedTask);
        _midiaRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _pdfMock.Setup(p => p.Renderizar(It.IsAny<string>(), It.IsAny<string>())).Returns([1, 2, 3]);
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(true);

        _assinaturaMock.Setup(a => a.AssinarAsync(It.IsAny<SolicitacaoDeAssinaturaDto>()))
            .ReturnsAsync(new AssinaturaDto("NomeDigitado", "Assinado por Dr. Vet - CRMV 12345-SP",
                DateTime.UtcNow, HabilitaDispensacaoExterna: false));
    }

    private DocumentoService CriarServico(params IDocumentoFactory[] factories) =>
        new(_docRepoMock.Object, _consultaRepoMock.Object, _vetRepoMock.Object,
            _animalRepoMock.Object, _tutorRepoMock.Object, _pagamentoRepoMock.Object,
            _auditoriaMock.Object, _midiaRepoMock.Object, _storageMock.Object,
            _pdfMock.Object, _assinaturaMock.Object, _usuarioMock.Object, factories);

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

    /// <summary>
    /// O conteudo aprovado vive na trilha de auditoria: e o registro do que o
    /// veterinario de fato aceitou (RN-082/RN-083).
    /// </summary>
    private void ConteudoAprovado(Guid consultaId, ConteudoDoProntuarioDto? conteudo = null)
    {
        var json = JsonSerializer.Serialize(
            conteudo ?? new ConteudoDoProntuarioDto
            {
                Anamnese = "Vomito ha 24h.",
                ExameFisico = "Abdome sensivel.",
                HipotesesDiagnosticas = ["Gastrite aguda"],
                Conduta = "Jejum de 12h e antiemetico.",
                Orientacoes = "Retornar se persistir."
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        _auditoriaMock.Setup(a => a.ObterDaConsultaAsync(consultaId)).ReturnsAsync(
            [new LogAuditoriaIa(consultaId, null, null, Guid.NewGuid(),
                DecisaoSobreRascunho.Aprovado, json, null, false, "ollama/llama3.1")]);
    }

    /// <summary>Vet, animal e Responsavel do cenario padrao.</summary>
    private void CenarioCompleto(Consulta consulta)
    {
        _consultaRepoMock.Setup(r => r.ObterPorIdAsync(consulta.Id)).ReturnsAsync(consulta);

        var vet = new Veterinario("Dr. Vet", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync(vet);

        var animal = new Animal("Rex", "Canino", "Labrador", new DateTime(2020, 1, 1), Guid.NewGuid());
        animal.RegistrarPeso(28m);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>())).ReturnsAsync(animal);

        _tutorRepoMock.Setup(r => r.ObterPorIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new Tutor("Ana Souza", "ana@exemplo.com", "11999998888"));

        ConteudoAprovado(consulta.Id);
    }

    // ── Conteúdo, assinatura e publicação (RN-083, RN-087, RN-090) ──────────

    private static Documento CriarDocumento(TipoDocumento tipo = TipoDocumento.Prontuario) =>
        new(tipo, "12345-SP", consultaId: Guid.NewGuid());

    [Fact]
    public void RegistrarConteudo_ConteudoVazio_LancaArgumentException()
    {
        var doc = CriarDocumento();

        Assert.Throws<ArgumentException>(() => doc.RegistrarConteudo("   "));
    }

    [Fact]
    public void RegistrarConteudo_GravaOTextoDoDocumento()
    {
        var doc = CriarDocumento();

        doc.RegistrarConteudo("PRONTUARIO — queixa: vomito ha 24h.");

        Assert.Contains("vomito", doc.Conteudo);
    }

    [Fact]
    public void RegistrarAssinatura_MarcaAssinadoEGuardaMetodoECarimbo()
    {
        var doc = CriarDocumento(TipoDocumento.ReceitaVeterinaria);

        doc.RegistrarAssinatura("NomeDigitado", "Assinado por Dra. Marina Costa — CRMV 12345-SP");

        Assert.True(doc.AssinadoDigitalmente);
        Assert.Equal("NomeDigitado", doc.AssinaturaMetodo);
        Assert.Contains("CRMV 12345-SP", doc.AssinaturaCarimbo);
    }

    [Fact]
    public void DefinirSubtipoAtestado_EmDocumentoQueNaoEAtestado_LancaInvalidOperation()
    {
        var doc = CriarDocumento(TipoDocumento.Prontuario);

        Assert.Throws<InvalidOperationException>(() => doc.DefinirSubtipoAtestado(TipoAtestado.Saude));
    }

    [Fact]
    public void DefinirSubtipoAtestado_EmAtestado_GuardaOSubtipo()
    {
        var doc = CriarDocumento(TipoDocumento.Atestado);

        doc.DefinirSubtipoAtestado(TipoAtestado.Saude);

        Assert.Equal(TipoAtestado.Saude, doc.Subtipo);
    }

    [Fact]
    public void Publicar_EIdempotente_EPreservaADataOriginal()
    {
        var doc = CriarDocumento();
        var primeiraPublicacao = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

        doc.Publicar(primeiraPublicacao);
        doc.Publicar(primeiraPublicacao.AddDays(2));

        // A data da publicacao e a referencia da notificacao ao Responsavel (RN-090)
        Assert.Equal(primeiraPublicacao, doc.PublicadoEm);
    }

    [Fact]
    public void MarcarComoLido_GuardaSomenteAPrimeiraLeitura()
    {
        var doc = CriarDocumento();
        var primeiraLeitura = new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc);

        doc.MarcarComoLido(primeiraLeitura);
        doc.MarcarComoLido(primeiraLeitura.AddHours(5));

        Assert.Equal(primeiraLeitura, doc.LidoEm);
    }

    [Fact]
    public async Task Gerar_SelecionaFactoryCorreta_PorTipoDocumento()
    {
        // Arrange — duas factories registradas; deve escolher a de Prontuario
        var documento = new Documento(TipoDocumento.Prontuario, "12345-SP", Guid.NewGuid());
        documento.RegistrarConteudo("conteudo formatado");

        var factoryProntuario = new Mock<IDocumentoFactory>();
        factoryProntuario.Setup(f => f.TipoSuportado).Returns(TipoDocumento.Prontuario);
        factoryProntuario
            .Setup(f => f.Criar(It.IsAny<ContextoDoDocumentoDto>()))
            .Returns(documento);

        var factoryReceita = new Mock<IDocumentoFactory>();
        factoryReceita.Setup(f => f.TipoSuportado).Returns(TipoDocumento.ReceitaVeterinaria);

        var consulta = CriarConsultaValidada();
        CenarioCompleto(consulta);

        var service = CriarServico(factoryProntuario.Object, factoryReceita.Object);

        // Act
        var resultado = await service.GerarAsync(consulta.Id, TipoDocumento.Prontuario);

        // Assert — a factory de Prontuario foi chamada; a de Receita nao
        factoryProntuario.Verify(f => f.Criar(It.IsAny<ContextoDoDocumentoDto>()), Times.Once);
        factoryReceita.Verify(f => f.Criar(It.IsAny<ContextoDoDocumentoDto>()), Times.Never);
        Assert.Equal(TipoDocumento.Prontuario, resultado.TipoDocumento);
    }

    [Fact]
    public async Task Gerar_EntregaAFactoryOConteudoAprovadoPeloVeterinario()
    {
        ContextoDoDocumentoDto? contexto = null;

        var factory = new Mock<IDocumentoFactory>();
        factory.Setup(f => f.TipoSuportado).Returns(TipoDocumento.Prontuario);
        factory.Setup(f => f.Criar(It.IsAny<ContextoDoDocumentoDto>()))
            .Callback<ContextoDoDocumentoDto>(c => contexto = c)
            .Returns(() =>
            {
                var doc = new Documento(TipoDocumento.Prontuario, "12345-SP", Guid.NewGuid());
                doc.RegistrarConteudo("x");
                return doc;
            });

        var consulta = CriarConsultaValidada();
        CenarioCompleto(consulta);

        await CriarServico(factory.Object).GerarAsync(consulta.Id, TipoDocumento.Prontuario);

        // RN-083: o que vai ao documento e o estado final aprovado, e nao o rascunho
        Assert.Equal("Vomito ha 24h.", contexto!.Conteudo.Anamnese);
        Assert.Equal("Gastrite aguda", contexto.Conteudo.HipotesesDiagnosticas[0]);

        // Dados que so o documento carrega: quem assina, de quem e o animal
        Assert.Equal("12345-SP", contexto.Crmv);
        Assert.Equal("Ana Souza", contexto.TutorNome);
        Assert.Equal(28m, contexto.PesoKg);
    }

    [Fact]
    public async Task Gerar_SemConteudoAprovado_NaoEmiteDocumento()
    {
        var factory = new Mock<IDocumentoFactory>();
        factory.Setup(f => f.TipoSuportado).Returns(TipoDocumento.Prontuario);

        var consulta = CriarConsultaValidada();
        CenarioCompleto(consulta);

        // A recusa grava conteudo vazio de proposito: nao houve conteudo aceito
        _auditoriaMock.Setup(a => a.ObterDaConsultaAsync(consulta.Id)).ReturnsAsync(
            [new LogAuditoriaIa(consulta.Id, null, null, Guid.NewGuid(),
                DecisaoSobreRascunho.NaoAprovado, string.Empty, "nao corresponde", true, null)]);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico(factory.Object).GerarAsync(consulta.Id, TipoDocumento.Prontuario));

        Assert.Equal("RN-083", ex.Codigo);
        factory.Verify(f => f.Criar(It.IsAny<ContextoDoDocumentoDto>()), Times.Never);
    }

    [Fact]
    public async Task Gerar_AnexaOPdfRenderizadoAoDocumento()
    {
        var factory = new Mock<IDocumentoFactory>();
        factory.Setup(f => f.TipoSuportado).Returns(TipoDocumento.Prontuario);
        factory.Setup(f => f.Criar(It.IsAny<ContextoDoDocumentoDto>())).Returns(() =>
        {
            var doc = new Documento(TipoDocumento.Prontuario, "12345-SP", Guid.NewGuid());
            doc.RegistrarConteudo("PRONTUARIO VETERINARIO");
            return doc;
        });

        var consulta = CriarConsultaValidada();
        CenarioCompleto(consulta);

        Midia? midia = null;
        _midiaRepoMock.Setup(r => r.AdicionarAsync(It.IsAny<Midia>()))
            .Callback<Midia>(m => midia = m).Returns(Task.CompletedTask);

        var resultado = await CriarServico(factory.Object).GerarAsync(consulta.Id, TipoDocumento.Prontuario);

        // O PDF e o que o Responsavel leva para fora do app (RN-090)
        Assert.NotNull(resultado.PdfMidiaId);
        Assert.Equal(midia!.Id, resultado.PdfMidiaId);
        Assert.Equal(TipoMidia.DocumentoPdf, midia.Tipo);

        // Entra pelo mesmo registro de midia dos outros arquivos: URL sempre temporaria
        _storageMock.Verify(s => s.GravarAsync(midia.ChaveStorage, It.IsAny<byte[]>(), "application/pdf"), Times.Once);
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

    // ── Publicação no board do pet (RN-011/RN-090) ───────────────────────────

    private Documento DocumentoPublicavel(TipoDocumento tipo = TipoDocumento.Prontuario)
    {
        var doc = new Documento(tipo, "12345-SP", consultaId: Guid.NewGuid());
        doc.RegistrarConteudo("PRONTUARIO VETERINARIO");

        _docRepoMock.Setup(r => r.ObterPorIdAsync(doc.Id)).ReturnsAsync(doc);

        return doc;
    }

    [Fact]
    public async Task Publicar_ColocaODocumentoNoBoardDoPet()
    {
        var doc = DocumentoPublicavel();

        var resultado = await CriarServico().PublicarAsync(doc.Id);

        Assert.NotNull(resultado.PublicadoEm);
    }

    [Fact]
    public async Task Publicar_DocumentoSemConteudo_NaoVaiAoBoard()
    {
        var doc = new Documento(TipoDocumento.Prontuario, "12345-SP", consultaId: Guid.NewGuid());
        _docRepoMock.Setup(r => r.ObterPorIdAsync(doc.Id)).ReturnsAsync(doc);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CriarServico().PublicarAsync(doc.Id));

        Assert.Equal("RN-090", ex.Codigo);
    }

    [Fact]
    public async Task Publicar_ReceitaSemAssinatura_NaoVaiAoBoard()
    {
        var receita = DocumentoPublicavel(TipoDocumento.ReceitaVeterinaria);

        // No board ela pareceria valida sem ser (RN-087)
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().PublicarAsync(receita.Id));

        Assert.Equal("RN-087", ex.Codigo);
    }

    [Fact]
    public async Task Publicar_ReceitaAssinada_VaiAoBoard()
    {
        var receita = DocumentoPublicavel(TipoDocumento.ReceitaVeterinaria);
        receita.RegistrarAssinatura("NomeDigitado", "Assinado por Dra. Marina — CRMV 12345-SP");

        var resultado = await CriarServico().PublicarAsync(receita.Id);

        Assert.NotNull(resultado.PublicadoEm);
    }

    [Fact]
    public async Task MarcarComoLido_DocumentoNaoPublicado_NaoEAceito()
    {
        var doc = DocumentoPublicavel();

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().MarcarComoLidoAsync(doc.Id));

        Assert.Equal("RN-090", ex.Codigo);
    }

    [Fact]
    public async Task MarcarComoLido_DepoisDePublicado_RegistraALeitura()
    {
        var doc = DocumentoPublicavel();
        var servico = CriarServico();

        await servico.PublicarAsync(doc.Id);
        var resultado = await servico.MarcarComoLidoAsync(doc.Id);

        // E o dado que diz se a orientacao chegou a quem cuida do animal
        Assert.NotNull(resultado.LidoEm);
    }

    [Fact]
    public async Task Board_DoAnimalDeOutroResponsavel_ERecusado()
    {
        var animal = new Animal("Rex", "Canino", "SRD", new DateTime(2021, 5, 1), Guid.NewGuid());
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);

        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhTutor).Returns(true);
        _usuarioMock.SetupGet(u => u.TutorId).Returns(Guid.NewGuid());

        // RN-105: o escopo vem do token, nunca do parametro da rota
        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterDoBoardDoPetAsync(animal.Id));

        Assert.Equal("RN-105", ex.Codigo);
    }

    [Fact]
    public async Task Board_DoProprioAnimal_TrazOsDocumentosPublicados()
    {
        var tutorId = Guid.NewGuid();
        var animal = new Animal("Rex", "Canino", "SRD", new DateTime(2021, 5, 1), tutorId);
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);

        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhTutor).Returns(true);
        _usuarioMock.SetupGet(u => u.TutorId).Returns(tutorId);

        var publicado = new Documento(TipoDocumento.Prontuario, "12345-SP", consultaId: Guid.NewGuid());
        publicado.RegistrarConteudo("x");
        publicado.Publicar(DateTime.UtcNow);

        _docRepoMock.Setup(r => r.ObterPublicadosPorAnimalAsync(animal.Id)).ReturnsAsync([publicado]);

        var board = await CriarServico().ObterDoBoardDoPetAsync(animal.Id);

        Assert.Single(board);
    }

    [Fact]
    public async Task Board_DoAnimalQueOVeterinarioNaoAtende_ERecusado()
    {
        var animal = new Animal("Rex", "Canino", "SRD", new DateTime(2021, 5, 1), Guid.NewGuid());
        _animalRepoMock.Setup(r => r.ObterPorIdAsync(animal.Id)).ReturnsAsync(animal);

        var vetId = Guid.NewGuid();
        _usuarioMock.SetupGet(u => u.EhAdmin).Returns(false);
        _usuarioMock.SetupGet(u => u.EhVeterinario).Returns(true);
        _usuarioMock.SetupGet(u => u.VeterinarioId).Returns(vetId);
        _animalRepoMock.Setup(r => r.VeterinarioAtendeAnimalAsync(vetId, animal.Id)).ReturnsAsync(false);

        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterDoBoardDoPetAsync(animal.Id));
    }
}
