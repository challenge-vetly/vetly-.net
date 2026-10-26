using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Moq;
using Vetly.API.HealthChecks;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Jobs;
using Vetly.Infrastructure.NoSql;

namespace Vetly.IntegrationTests;

/// <summary>
/// Liga o teste só quando há um MongoDB para ele (<c>VETLY_TESTE_MONGO</c> com a
/// string de conexão). Sem a variável, o teste aparece como pulado — a suíte padrão
/// não depende de um banco de documentos de pé, como manda o adaptador em memória.
/// </summary>
public sealed class FatoComMongoAttribute : FactAttribute
{
    /// <summary>Variável de ambiente com a string de conexão do Mongo de teste.</summary>
    public const string Variavel = "VETLY_TESTE_MONGO";

    public FatoComMongoAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variavel)))
            Skip = $"Defina {Variavel} com a string de conexao de um MongoDB para rodar este teste.";
    }
}

/// <summary>
/// Histórico diário do avatar (RN-114): os dois adaptadores da porta, o mapeamento do
/// documento, a sonda de saúde e a rotina diária.
/// </summary>
public class HistoricoDoAvatarTests
{
    private static readonly Guid Animal = Guid.NewGuid();

    private static RegistroDiarioDoAvatarDto Registro(Guid animalId, int diasAtras, int saude, int humor = 80) => new()
    {
        AnimalId = animalId,
        Data = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-diasAtras),
        Saude = saude,
        Humor = humor,
        Estado = EstadoDoAvatar.Saudavel,
        NivelDoPelo = 0,
        Motivos = [MotivoDoAvatar.CheckUpVencido],
        CalculadoEm = DateTime.UtcNow.AddDays(-diasAtras)
    };

    private static readonly CriterioDeOrdenacao DataDesc = new(CamposDeOrdenacao.HistoricoDoAvatar.Data, true);

    // ── Adaptador em memória ─────────────────────────────────────────────────

    [Fact]
    public async Task EmMemoria_DuasGravacoesNoMesmoDia_FicaUmRegistroComOUltimoValor()
    {
        // Arrange
        var repositorio = new HistoricoDoAvatarRepositoryEmMemoria();

        // Act
        await repositorio.RegistrarAsync(Registro(Animal, 0, saude: 70));
        await repositorio.RegistrarAsync(Registro(Animal, 0, saude: 90));
        var pagina = await repositorio.ObterPaginadoAsync(Animal, new Paginacao(), DataDesc);

        // Assert — a mesma regra do indice unico (animalId, data) do Mongo
        var registro = Assert.Single(pagina.Itens);
        Assert.Equal(90, registro.Saude);
    }

    [Fact]
    public async Task EmMemoria_OrdenaEPaginaSemMisturarAnimais()
    {
        // Arrange
        var repositorio = new HistoricoDoAvatarRepositoryEmMemoria();
        var outro = Guid.NewGuid();
        foreach (var (dias, saude) in new[] { (0, 50), (1, 90), (2, 70) })
            await repositorio.RegistrarAsync(Registro(Animal, dias, saude));
        await repositorio.RegistrarAsync(Registro(outro, 0, 10));

        // Act
        var piores = await repositorio.ObterPaginadoAsync(Animal, new Paginacao { Tamanho = 2 },
            new CriterioDeOrdenacao(CamposDeOrdenacao.HistoricoDoAvatar.Saude, false));
        var maisAntigos = await repositorio.ObterPaginadoAsync(Animal, new Paginacao(),
            new CriterioDeOrdenacao(CamposDeOrdenacao.HistoricoDoAvatar.Data, false));

        // Assert
        Assert.Equal(3, piores.Total);
        Assert.Equal([50, 70], piores.Itens.Select(r => r.Saude));
        Assert.Equal([70, 90, 50], maisAntigos.Itens.Select(r => r.Saude));
    }

    [Theory]
    [InlineData(CamposDeOrdenacao.HistoricoDoAvatar.Humor, true, new[] { 95, 60, 30 })]
    [InlineData(CamposDeOrdenacao.HistoricoDoAvatar.Humor, false, new[] { 30, 60, 95 })]
    [InlineData(CamposDeOrdenacao.HistoricoDoAvatar.Saude, true, new[] { 60, 30, 95 })]
    public async Task EmMemoria_OrdenaPorCadaCampoDaWhitelist(string campo, bool descendente, int[] humoresEsperados)
    {
        // Arrange — saude e humor descasados para cada ordenacao dar uma sequencia propria
        var repositorio = new HistoricoDoAvatarRepositoryEmMemoria();
        var animal = Guid.NewGuid();
        await repositorio.RegistrarAsync(Registro(animal, 0, saude: 40, humor: 95));
        await repositorio.RegistrarAsync(Registro(animal, 1, saude: 90, humor: 60));
        await repositorio.RegistrarAsync(Registro(animal, 2, saude: 60, humor: 30));

        // Act
        var pagina = await repositorio.ObterPaginadoAsync(animal, new Paginacao(), new CriterioDeOrdenacao(campo, descendente));

        // Assert
        Assert.Equal(humoresEsperados, pagina.Itens.Select(r => r.Humor));
    }

    [Fact]
    public async Task EmMemoria_RegistroDevolvidoNaoAlteraOGravado()
    {
        // Arrange
        var repositorio = new HistoricoDoAvatarRepositoryEmMemoria();
        await repositorio.RegistrarAsync(Registro(Animal, 0, saude: 80));
        var lido = (await repositorio.ObterPaginadoAsync(Animal, new Paginacao(), DataDesc)).Itens[0];

        // Act
        lido.Saude = 0;
        lido.Motivos.Clear();
        var relido = (await repositorio.ObterPaginadoAsync(Animal, new Paginacao(), DataDesc)).Itens[0];

        // Assert
        Assert.Equal(80, relido.Saude);
        Assert.Single(relido.Motivos);
    }

    // ── Documento do Mongo ───────────────────────────────────────────────────

    [Fact]
    public void Documento_IdaEVolta_PreservaOsDados()
    {
        // Arrange
        var original = Registro(Animal, 3, saude: 65, humor: 40);

        // Act
        var documento = HistoricoDoAvatarRepositoryMongo.ParaDocumento(original);
        var volta = HistoricoDoAvatarRepositoryMongo.ParaRegistro(documento);

        // Assert — data como texto ordenavel, estado e motivos pelo nome
        Assert.Equal(original.Data.ToString("yyyy-MM-dd"), documento.Data);
        Assert.Equal("Saudavel", documento.Estado);
        Assert.Equal(["CheckUpVencido"], documento.Motivos);
        Assert.Null(documento.Id);
        Assert.Equal(original.AnimalId, volta.AnimalId);
        Assert.Equal(original.Data, volta.Data);
        Assert.Equal(65, volta.Saude);
        Assert.Equal(40, volta.Humor);
        Assert.Equal(DateTimeKind.Utc, volta.CalculadoEm.Kind);
    }

    [Fact]
    public void Documento_MotivoDesconhecido_EIgnoradoEmVezDeQuebrarALeitura()
    {
        // Arrange — gravado por uma versao futura da API
        var documento = HistoricoDoAvatarRepositoryMongo.ParaDocumento(Registro(Animal, 0, 70));
        documento.Motivos.Add("MotivoQueAindaNaoExiste");

        // Act
        var registro = HistoricoDoAvatarRepositoryMongo.ParaRegistro(documento);

        // Assert
        Assert.Equal([MotivoDoAvatar.CheckUpVencido], registro.Motivos);
    }

    // ── Sonda de saúde ───────────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_SemServidor_FicaDegradadoSemTravar()
    {
        // Arrange — porta sem ninguem escutando e selecao de servidor curta
        var configuracao = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1");
        configuracao.ServerSelectionTimeout = TimeSpan.FromSeconds(1);
        var banco = new MongoClient(configuracao).GetDatabase("vetly-teste");

        // Act
        var resultado = await new MongoDbHealthCheck(banco).CheckHealthAsync(new HealthCheckContext());

        // Assert — Degraded, nao Unhealthy: sem Mongo o avatar continua no ar
        Assert.Equal(HealthStatus.Degraded, resultado.Status);
    }

    // ── Rotina diária ────────────────────────────────────────────────────────

    [Fact]
    public async Task Rotina_RegistraTodosOsAnimaisAtivos_MesmoComUmFalhando()
    {
        // Arrange
        var tutor = Guid.NewGuid();
        var thor = new Animal("Thor", "Canino", "SRD", new DateTime(2022, 1, 1), tutor);
        var mel = new Animal("Mel", "Felino", "SRD", new DateTime(2021, 1, 1), tutor);

        var animais = new Mock<IAnimalRepository>();
        animais.Setup(r => r.ObterAtivosAsync()).ReturnsAsync([thor, mel]);

        var obrigacoes = new Mock<IObrigacaoRepository>();
        obrigacoes.Setup(r => r.ObterDoAnimalAsync(thor.Id, true)).ThrowsAsync(new InvalidOperationException("falha"));
        obrigacoes.Setup(r => r.ObterDoAnimalAsync(mel.Id, true)).ReturnsAsync([]);

        var historico = new HistoricoDoAvatarRepositoryEmMemoria();
        var rotina = new RegistrarCondicaoDosAvatares(
            animais.Object, new CalculadoraDoAvatar(obrigacoes.Object, Mock.Of<IConsultaRepository>()),
            historico, NullLogger<RegistrarCondicaoDosAvatares>.Instance);

        // Act
        var registrados = await rotina.ExecutarAsync(CancellationToken.None);

        // Assert — a falha do Thor nao impede o registro da Mel
        Assert.Equal(1, registrados);
        Assert.Equal(TimeSpan.FromHours(24), rotina.Intervalo);
        Assert.Single((await historico.ObterPaginadoAsync(mel.Id, new Paginacao(), DataDesc)).Itens);
        Assert.Empty((await historico.ObterPaginadoAsync(thor.Id, new Paginacao(), DataDesc)).Itens);
    }

    // ── MongoDB real (opcional: VETLY_TESTE_MONGO) ───────────────────────────

    private static IMongoDatabase BancoDeTeste() =>
        new MongoClient(Environment.GetEnvironmentVariable(FatoComMongoAttribute.Variavel))
            .GetDatabase($"vetly-teste-{Guid.NewGuid():N}");

    [FatoComMongo]
    public async Task Mongo_UpsertDoMesmoDia_FicaUmDocumentoEOrdenaPorSaude()
    {
        // Arrange
        var banco = BancoDeTeste();
        var repositorio = new HistoricoDoAvatarRepositoryMongo(banco);
        var animal = Guid.NewGuid();

        try
        {
            // Act
            await repositorio.RegistrarAsync(Registro(animal, 0, saude: 70));
            await repositorio.RegistrarAsync(Registro(animal, 0, saude: 85));
            await repositorio.RegistrarAsync(Registro(animal, 1, saude: 40));
            var pagina = await repositorio.ObterPaginadoAsync(animal, new Paginacao(),
                new CriterioDeOrdenacao(CamposDeOrdenacao.HistoricoDoAvatar.Saude, false));

            // Assert
            Assert.Equal(2, pagina.Total);
            Assert.Equal([40, 85], pagina.Itens.Select(r => r.Saude));
        }
        finally
        {
            await banco.Client.DropDatabaseAsync(banco.DatabaseNamespace.DatabaseName);
        }
    }

    [FatoComMongo]
    public async Task Mongo_CriaOIndiceUnicoPorAnimalEDia()
    {
        // Arrange
        var banco = BancoDeTeste();
        var repositorio = new HistoricoDoAvatarRepositoryMongo(banco);

        try
        {
            // Act
            await repositorio.RegistrarAsync(Registro(Guid.NewGuid(), 0, 90));
            var indices = await (await banco.GetCollection<DocumentoDoAvatar>(HistoricoDoAvatarRepositoryMongo.Colecao)
                .Indexes.ListAsync()).ToListAsync();

            // Assert
            var unico = Assert.Single(indices, i => i["name"] == "ux_animal_data");
            Assert.True(unico["unique"].AsBoolean);
        }
        finally
        {
            await banco.Client.DropDatabaseAsync(banco.DatabaseNamespace.DatabaseName);
        }
    }

    [FatoComMongo]
    public async Task Mongo_HealthCheckComServidor_FicaSaudavel()
    {
        // Arrange
        var banco = BancoDeTeste();

        // Act
        var resultado = await new MongoDbHealthCheck(banco).CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Healthy, resultado.Status);
    }
}
