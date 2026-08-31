using Moq;
using Vetly.Application.DTOs.Comum;
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
/// Cobre RN-107 (validação de CRMV e bloqueio de publicação), RN-022/RN-025 (retorno de
/// agendamentos futuros no soft delete), RN-026 (endereço) e RN-033/RN-057 (reputação).
/// </summary>
public class VeterinarioServiceTests
{
    private readonly Mock<IVeterinarioRepository> _repoMock = new();
    private readonly Mock<ICrmvAdapter> _crmvAdapterMock = new();
    private readonly Mock<ISenhaHasher> _hasherMock = new();
    private readonly Mock<IGeradorDeSenhaTemporaria> _geradorDeSenhaMock = new();
    private readonly Mock<IGeocodificacaoAdapter> _geocodificacaoMock = new();
    private readonly Mock<IConsultaRepository> _consultaRepoMock = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepoMock = new();
    private readonly Mock<IUsuarioAtual> _usuarioMock = new();

    private VeterinarioService CriarServico() =>
        new(_repoMock.Object, _crmvAdapterMock.Object, _hasherMock.Object,
            _geradorDeSenhaMock.Object, _geocodificacaoMock.Object,
            _consultaRepoMock.Object, _pagamentoRepoMock.Object, _usuarioMock.Object);

    /// <summary>Configura a resposta da geocodificacao para o proximo endereco (RN-026).</summary>
    private void GeocodificacaoResolve(decimal lat = -23.561414m, decimal lng = -46.655881m, bool revisar = false) =>
        _geocodificacaoMock
            .Setup(g => g.GeocodificarAsync(It.IsAny<EnderecoDto>()))
            .ReturnsAsync(new CoordenadaDto
            {
                Latitude = lat, Longitude = lng,
                Precisao = revisar ? PrecisaoCoordenada.Bairro : PrecisaoCoordenada.Cep,
                Revisar = revisar
            });

    /// <summary>Configura a resposta do conselho para o proximo cadastro (RN-107).</summary>
    private void ConselhoResponde(ResultadoValidacaoCrmv resultado) =>
        _crmvAdapterMock
            .Setup(a => a.ValidarRegistroAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new ResultadoCrmvDto
            {
                Resultado = resultado,
                ConsultadoEm = DateTime.UtcNow
            });

    public VeterinarioServiceTests()
    {
        ConselhoResponde(ResultadoValidacaoCrmv.Valido);
        _hasherMock.Setup(h => h.GerarHash(It.IsAny<string>())).Returns("hash-da-senha-temporaria");
        _geradorDeSenhaMock.Setup(g => g.Gerar()).Returns("SenhaTemp123");
        _repoMock.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>())).ReturnsAsync((Veterinario?)null);
        GeocodificacaoResolve();
    }

    private static CriarVeterinarioDto CriarDto(string crmv = "12345-SP") => new()
    {
        Nome = "Dr. Teste",
        Crmv = crmv,
        UfAtuacao = "SP",
        Email = $"vet-{Guid.NewGuid():N}@exemplo.com",
        Persona = PersonaVeterinario.Autonomo,
        Plano = PlanoAssinatura.Profissional
    };

    private static Veterinario CriarVeterinario() =>
        new("Dr. Teste", new Crmv("12345-SP"), "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

    // ── Validação de CRMV junto ao conselho (RN-107, C-05) ───────────────────

    [Fact]
    public async Task CriarAsync_ConselhoRespondeValido_PublicaOPerfilNoMatching()
    {
        ConselhoResponde(ResultadoValidacaoCrmv.Valido);
        _repoMock.Setup(r => r.ObterPorCrmvAsync(It.IsAny<string>())).ReturnsAsync((Veterinario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Veterinario>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = (await CriarServico().CriarAsync(CriarDto("11111-SP"))).Veterinario;

        Assert.Equal(StatusCrmv.Valido, resultado.CrmvStatus);
        Assert.True(resultado.Publicado);
        Assert.NotNull(resultado.PublicadoEm);
    }

    [Theory]
    [InlineData(ResultadoValidacaoCrmv.Invalido, StatusCrmv.Invalido)]
    [InlineData(ResultadoValidacaoCrmv.Suspenso, StatusCrmv.Suspenso)]
    [InlineData(ResultadoValidacaoCrmv.Indisponivel, StatusCrmv.PendenteValidacao)]
    public async Task CriarAsync_ConselhoNaoConfirma_NaoPublicaOPerfil(
        ResultadoValidacaoCrmv respostaDoConselho, StatusCrmv statusEsperado)
    {
        ConselhoResponde(respostaDoConselho);
        _repoMock.Setup(r => r.ObterPorCrmvAsync(It.IsAny<string>())).ReturnsAsync((Veterinario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Veterinario>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = (await CriarServico().CriarAsync(CriarDto("11111-SP"))).Veterinario;

        Assert.Equal(statusEsperado, resultado.CrmvStatus);
        // Indisponibilidade do conselho nao aprova por omissao (RN-107)
        Assert.False(resultado.Publicado);
    }

    [Fact]
    public async Task RevalidarCrmvAsync_ConselhoVoltaAResponder_TiraOPerfilDaPendencia()
    {
        var vet = CriarVeterinario();
        vet.RegistrarValidacaoCrmv(StatusCrmv.PendenteValidacao, DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Veterinario>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        ConselhoResponde(ResultadoValidacaoCrmv.Valido);

        var resultado = await CriarServico().RevalidarCrmvAsync(vet.Id);

        Assert.Equal(ResultadoValidacaoCrmv.Valido, resultado.Resultado);
        Assert.Equal(StatusCrmv.Valido, vet.CrmvStatus);
        Assert.True(vet.Publicado);
    }

    [Fact]
    public async Task RevalidarCrmvAsync_ConselhoSuspende_DespublicaPerfilAntesPublicado()
    {
        var vet = CriarVeterinario();
        vet.RegistrarValidacaoCrmv(StatusCrmv.Valido, DateTime.UtcNow);
        vet.PublicarNoMatching(DateTime.UtcNow);

        _repoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Veterinario>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        ConselhoResponde(ResultadoValidacaoCrmv.Suspenso);

        await CriarServico().RevalidarCrmvAsync(vet.Id);

        Assert.Equal(StatusCrmv.Suspenso, vet.CrmvStatus);
        Assert.False(vet.Publicado);
    }

    // ── Endereço e coordenada (RN-026) ───────────────────────────────────────

    [Fact]
    public async Task CriarAsync_ComEndereco_PersisteEnderecoSemCoordenada()
    {
        _repoMock.Setup(r => r.ObterPorCrmvAsync(It.IsAny<string>())).ReturnsAsync((Veterinario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Veterinario>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = CriarDto("54321-SP");
        dto.Endereco = new EnderecoDto
        {
            Cep = "01310-100", Logradouro = "Av. Paulista", Numero = "1578",
            Bairro = "Bela Vista", Cidade = "Sao Paulo", Uf = "sp",
            // Coordenada informada pelo cliente deve ser ignorada (RN-026)
            Latitude = 99, Longitude = 99
        };

        var resultado = (await CriarServico().CriarAsync(dto)).Veterinario;

        Assert.NotNull(resultado.Endereco);
        Assert.Equal("01310-100", resultado.Endereco!.Cep);
        Assert.Equal("SP", resultado.Endereco.Uf);
        // A coordenada vem da geocodificacao do endereco persistido, nao do payload:
        // o cliente mandou 99/99 e foi ignorado (RN-026)
        Assert.Equal(-23.561414m, resultado.Endereco.Latitude);
        Assert.Equal(-46.655881m, resultado.Endereco.Longitude);
    }

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    public void DefinirCoordenada_ForaDeFaixa_LancaArgumentOutOfRange(decimal lat, decimal lng)
    {
        var endereco = new Endereco("01310-100", "Av. Paulista", "1578", "Bela Vista", "Sao Paulo", "SP");

        Assert.Throws<ArgumentOutOfRangeException>(() => endereco.DefinirCoordenada(lat, lng));
    }

    [Fact]
    public void DefinirCoordenada_ValoresValidos_MarcaEnderecoComoGeocodificado()
    {
        var endereco = new Endereco("01310-100", "Av. Paulista", "1578", "Bela Vista", "Sao Paulo", "SP");

        Assert.False(endereco.TemCoordenada());

        endereco.DefinirCoordenada(-23.561414m, -46.655881m, revisar: true);

        Assert.True(endereco.TemCoordenada());
        Assert.True(endereco.CoordenadaRevisar);
    }

    // ── Publicação no matching e CRMV (RN-107, RN-033) ───────────────────────

    [Fact]
    public void Veterinario_RecemCriado_NasceComCrmvPendenteENaoPublicado()
    {
        var vet = CriarVeterinario();

        Assert.Equal(StatusCrmv.PendenteValidacao, vet.CrmvStatus);
        Assert.False(vet.Publicado);
    }

    [Fact]
    public void PublicarNoMatching_CrmvPendente_NaoPublica()
    {
        var vet = CriarVeterinario();

        var publicou = vet.PublicarNoMatching(DateTime.UtcNow);

        Assert.False(publicou);
        Assert.False(vet.Publicado);
        Assert.Null(vet.PublicadoEm);
    }

    [Fact]
    public void PublicarNoMatching_CrmvValido_PublicaEPreservaDataOriginal()
    {
        var vet = CriarVeterinario();
        vet.RegistrarValidacaoCrmv(StatusCrmv.Valido, DateTime.UtcNow);

        var primeiraPublicacao = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(vet.PublicarNoMatching(primeiraPublicacao));

        // Republicar nao reinicia o selo "Novo na Vetly", que conta 30 dias da 1a publicacao (RN-033)
        vet.PublicarNoMatching(primeiraPublicacao.AddDays(10));

        Assert.True(vet.Publicado);
        Assert.Equal(primeiraPublicacao, vet.PublicadoEm);
    }

    [Theory]
    [InlineData(StatusCrmv.Invalido)]
    [InlineData(StatusCrmv.Suspenso)]
    [InlineData(StatusCrmv.PendenteValidacao)]
    public void RegistrarValidacaoCrmv_ResultadoNaoValido_DespublicaOPerfil(StatusCrmv status)
    {
        var vet = CriarVeterinario();
        vet.RegistrarValidacaoCrmv(StatusCrmv.Valido, DateTime.UtcNow);
        vet.PublicarNoMatching(DateTime.UtcNow);

        vet.RegistrarValidacaoCrmv(status, DateTime.UtcNow);

        Assert.False(vet.Publicado);
        Assert.Null(vet.PublicadoEm);
    }

    [Fact]
    public void Desativar_TiraOPerfilDoMatching()
    {
        var vet = CriarVeterinario();
        vet.RegistrarValidacaoCrmv(StatusCrmv.Valido, DateTime.UtcNow);
        vet.PublicarNoMatching(DateTime.UtcNow);

        vet.Desativar();

        Assert.False(vet.Ativo);
        Assert.False(vet.Publicado);
    }

    // ── Reputação (RN-057) ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public void TemNotaPublica_ExigeMinimoDeTresAvaliacoes(int numAvaliacoes, bool esperado)
    {
        var vet = CriarVeterinario();
        vet.AtualizarReputacao(4.7m, numAvaliacoes);

        Assert.Equal(esperado, vet.TemNotaPublica());
    }

    [Fact]
    public void AtualizarReputacao_NotaForaDaEscala_LancaArgumentOutOfRange()
    {
        var vet = CriarVeterinario();

        Assert.Throws<ArgumentOutOfRangeException>(() => vet.AtualizarReputacao(5.1m, 10));
    }

    [Fact]
    public async Task CriarAsync_CrmvValido_RetornaVeterinarioDtoComIdPreenchido()
    {
        _repoMock.Setup(r => r.ObterPorCrmvAsync("12345-SP")).ReturnsAsync((Veterinario?)null);
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Veterinario>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = (await CriarServico().CriarAsync(CriarDto("12345-SP"))).Veterinario;

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

        Assert.Equal("RN-107", ex.Codigo);
    }

    [Fact]
    public async Task DesativarAsync_ComConsultaFutura_RetornaAgendamentoEDesativaVet()
    {
        var crmv = new Crmv("12345-SP");
        var vet = new Veterinario("Dr. Vet", crmv, "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
        var consultaFutura = new Consulta(
            DateTime.UtcNow.AddDays(3), ModalidadeAtendimento.Presencial,
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
