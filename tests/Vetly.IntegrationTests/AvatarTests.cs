using System.Net;
using static Vetly.IntegrationTests.AtoresDeTeste;

namespace Vetly.IntegrationTests;

/// <summary>
/// Avatar de ponta a ponta (RN-112 a RN-114): pet cadastrado pelo app, obrigação
/// vencida derrubando a saúde, o board trazendo o mesmo avatar e o escopo do
/// Responsável.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class AvatarTests
{
    private readonly HttpClient _client;

    public AvatarTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private async Task CriarObrigacaoAsync(Responsavel dono, Guid animalId, string tipo, int diasDesdeOVencimento)
    {
        var vencimento = DateTime.UtcNow.AddDays(-diasDesdeOVencimento).ToString("O");
        var resposta = await EnviarAsync(_client, HttpMethod.Post, $"/api/obrigacoes/animal/{animalId}", dono.Token,
            $$"""{"tipo":"{{tipo}}","descricao":"{{tipo}}","proximoVencimento":"{{vencimento}}","periodicidadeEmDias":30}""");
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
    }

    [Fact]
    public async Task PetNovo_SemConsulta_PerdeSoOPontoDaConsultaEFicaFeliz()
    {
        // Arrange — peso registrado no cadastro, nenhuma consulta realizada ainda
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var pet = await CriarPetAsync(_client, ana);

        // Act
        var avatar = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/avatar", ana.Token));

        // Assert
        Assert.Equal(90, avatar.GetProperty("saude").GetInt32());
        Assert.Equal(100, avatar.GetProperty("humor").GetInt32());
        Assert.Equal("Feliz", avatar.GetProperty("estado").GetString());
        Assert.Equal("SemConsultaHa12Meses", avatar.GetProperty("motivos")[0].GetString());
        Assert.True(avatar.GetProperty("spriteDisponivel").GetBoolean());
    }

    [Fact]
    public async Task VacinaVencida_DerrubaASaudeEMudaOEstado()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var pet = await CriarPetAsync(_client, ana);
        await CriarObrigacaoAsync(ana, pet, "Vacina", diasDesdeOVencimento: 20);

        // Act
        var avatar = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/avatar", ana.Token));

        // Assert — 100 − 40 − 5 (1 degrau de 15 dias) − 10 (sem consulta) = 45
        Assert.Equal(45, avatar.GetProperty("saude").GetInt32());
        Assert.Equal("VacinaAtrasada", avatar.GetProperty("estado").GetString());
        Assert.Equal("VacinaVencida", avatar.GetProperty("motivos")[0].GetString());
    }

    [Fact]
    public async Task HigienizacaoVencida_FazOPeloCrescer_EOBoardTrazOMesmoAvatar()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var pet = await CriarPetAsync(_client, ana);
        await CriarObrigacaoAsync(ana, pet, "Higienizacao", diasDesdeOVencimento: 31);

        // Act
        var avatar = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/avatar", ana.Token));
        var board = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/board", ana.Token));

        // Assert
        Assert.Equal(3, avatar.GetProperty("nivelDoPelo").GetInt32());
        Assert.Equal("HigieneAtrasada", avatar.GetProperty("estado").GetString());

        var doBoard = board.GetProperty("avatar");
        Assert.Equal(avatar.GetProperty("saude").GetInt32(), doBoard.GetProperty("saude").GetInt32());
        Assert.Equal("HigieneAtrasada", board.GetProperty("avatarEstado").GetString());
    }

    [Fact]
    public async Task Board_PetFeliz_MantemOCampoLegadoComOsTresValoresOriginais()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var pet = await CriarPetAsync(_client, ana);

        // Act
        var board = await JsonDe(await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/board", ana.Token));

        // Assert — "Feliz" so no campo novo
        Assert.Equal("Feliz", board.GetProperty("avatar").GetProperty("estado").GetString());
        Assert.Equal("Saudavel", board.GetProperty("avatarEstado").GetString());
    }

    [Fact]
    public async Task AvatarComHalJson_TrazLinksParaBoardEObrigacoes()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var pet = await CriarPetAsync(_client, ana);

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/avatar", ana.Token, accept: Hal);
        var corpo = await JsonDe(resposta);

        // Assert
        Assert.Equal(Hal, resposta.Content.Headers.ContentType!.MediaType);
        Assert.Equal(pet, corpo.GetProperty("dados").GetProperty("animalId").GetGuid());
        var links = corpo.GetProperty("_links");
        Assert.Contains("board", Rels(links));
        Assert.Contains("obrigacoes", Rels(links));
    }

    [Fact]
    public async Task AvatarDoPetDeOutroResponsavel_Retorna403()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var bruno = await CriarResponsavelAsync(_client, "Bruno");
        var petDoBruno = await CriarPetAsync(_client, bruno, "Rex");

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{petDoBruno}/avatar", ana.Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    // ── Historico diario (RN-114) ────────────────────────────────────────────

    [Fact]
    public async Task LerOAvatar_GravaODiaNoHistorico_UmRegistroPorDia()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var pet = await CriarPetAsync(_client, ana);

        // Act — duas leituras no mesmo dia
        await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/avatar", ana.Token);
        await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/avatar", ana.Token);
        var historico = await JsonDe(await EnviarAsync(_client, HttpMethod.Get,
            $"/api/animais/{pet}/avatar/historico", ana.Token));

        // Assert
        Assert.Equal(1, historico.GetProperty("total").GetInt32());
        var dia = historico.GetProperty("itens")[0];
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd"), dia.GetProperty("data").GetString());
        Assert.Equal(90, dia.GetProperty("saude").GetInt32());
    }

    [Fact]
    public async Task HistoricoComHalJson_TrazNavegacaoELinkParaOAvatar()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var pet = await CriarPetAsync(_client, ana);
        await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{pet}/avatar", ana.Token);

        // Act
        var corpo = await JsonDe(await EnviarAsync(_client, HttpMethod.Get,
            $"/api/animais/{pet}/avatar/historico?ordenarPor=saude&direcao=asc", ana.Token, accept: Hal));

        // Assert
        Assert.Contains("self", Rels(corpo.GetProperty("_links")));
        Assert.Contains("ordenarPor=saude", corpo.GetProperty("_links")[0].GetProperty("href").GetString());
        Assert.Equal(["avatar"], Rels(corpo.GetProperty("itens")[0].GetProperty("_links")));
    }

    [Fact]
    public async Task HistoricoDoPetDeOutroResponsavel_Retorna403()
    {
        // Arrange
        var ana = await CriarResponsavelAsync(_client, "Ana");
        var bruno = await CriarResponsavelAsync(_client, "Bruno");
        var petDoBruno = await CriarPetAsync(_client, bruno, "Rex");

        // Act
        var resposta = await EnviarAsync(_client, HttpMethod.Get, $"/api/animais/{petDoBruno}/avatar/historico", ana.Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }
}
