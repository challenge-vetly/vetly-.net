using Moq;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Empresa;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do EmpresaService e das regras da unidade:
/// politica de retencao configuravel (RN-042), plano e faixa Enterprise (RN-072)
/// e endereco embutido (RN-026).
/// </summary>
public class EmpresaServiceTests
{
    private readonly Mock<IEmpresaRepository> _repoMock = new();
    private readonly Mock<IVeterinarioRepository> _vetRepoMock = new();

    private EmpresaService CriarServico() => new(_repoMock.Object, _vetRepoMock.Object);

    private static CriarEmpresaDto CriarDto() => new()
    {
        Nome = "Clinica Vida Pet",
        Tipo = "Clinica",
        AdministradorId = Guid.NewGuid()
    };

    // ── Política de retenção (RN-042) ────────────────────────────────────────

    [Fact]
    public async Task CriarAsync_SemPercentualInformado_UsaOPadraoDeTrintaPorCento()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Empresa>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var resultado = await CriarServico().CriarAsync(CriarDto());

        Assert.Equal(30m, resultado.PercentualRetencaoParcial);
    }

    [Fact]
    public async Task CriarAsync_ComPercentualDaClinica_PersisteOValorConfigurado()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Empresa>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = CriarDto();
        dto.PercentualRetencaoParcial = 15m;

        var resultado = await CriarServico().CriarAsync(dto);

        Assert.Equal(15m, resultado.PercentualRetencaoParcial);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void DefinirPoliticaRetencao_ForaDaEscala_LancaArgumentOutOfRange(decimal percentual)
    {
        var empresa = new Empresa("Clinica", "Clinica", Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() => empresa.DefinirPoliticaRetencao(percentual));
    }

    // ── Faixa Enterprise (RN-072) ────────────────────────────────────────────

    [Theory]
    [InlineData(1, FaixaEnterprise.De1a5)]
    [InlineData(5, FaixaEnterprise.De1a5)]
    [InlineData(6, FaixaEnterprise.De6a10)]
    [InlineData(10, FaixaEnterprise.De6a10)]
    [InlineData(11, FaixaEnterprise.De11a20)]
    [InlineData(20, FaixaEnterprise.De11a20)]
    [InlineData(21, FaixaEnterprise.Acima20)]
    public void RecalcularFaixaEnterprise_TrocaAoCruzarOLimiteDeVets(int vets, FaixaEnterprise esperada)
    {
        var empresa = new Empresa("Clinica", "Clinica", Guid.NewGuid(), PlanoAssinatura.Enterprise);

        Assert.Equal(esperada, empresa.RecalcularFaixaEnterprise(vets));
    }

    [Fact]
    public void RecalcularFaixaEnterprise_ForaDoEnterprise_NaoTemFaixa()
    {
        var empresa = new Empresa("Clinica", "Clinica", Guid.NewGuid(), PlanoAssinatura.Profissional);

        Assert.Null(empresa.RecalcularFaixaEnterprise(30));
    }

    [Fact]
    public void AtualizarPlano_SaindoDoEnterprise_LimpaAFaixa()
    {
        var empresa = new Empresa("Clinica", "Clinica", Guid.NewGuid(), PlanoAssinatura.Enterprise);
        empresa.RecalcularFaixaEnterprise(8);

        empresa.AtualizarPlano(PlanoAssinatura.Profissional);

        Assert.Null(empresa.FaixaEnterprise);
    }

    [Fact]
    public async Task VincularVeterinarioAsync_RecalculaAFaixaEnterprise()
    {
        var empresa = new Empresa("Clinica", "Clinica", Guid.NewGuid(), PlanoAssinatura.Enterprise);
        var vet = new Veterinario("Dr. Teste", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Enterprise);

        // Apos a vinculacao a unidade passa a ter 6 vets — cruza para a faixa De6a10
        var vinculados = Enumerable.Range(0, 6).Select(_ =>
            new Veterinario("Vet", new Crmv("12345-SP"), "SP",
                PersonaVeterinario.Vinculado, PlanoAssinatura.Enterprise)).ToList();

        _repoMock.Setup(r => r.ObterPorIdAsync(empresa.Id)).ReturnsAsync(empresa);
        _repoMock.Setup(r => r.Atualizar(It.IsAny<Empresa>()));
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepoMock.Setup(r => r.ObterPorIdAsync(vet.Id)).ReturnsAsync(vet);
        _vetRepoMock.Setup(r => r.Atualizar(It.IsAny<Veterinario>()));
        _vetRepoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        _vetRepoMock.Setup(r => r.ObterPorEmpresaAsync(empresa.Id)).ReturnsAsync(vinculados);

        await CriarServico().VincularVeterinarioAsync(empresa.Id, vet.Id);

        Assert.Equal(FaixaEnterprise.De6a10, empresa.FaixaEnterprise);
        Assert.Equal(PersonaVeterinario.Vinculado, vet.Persona);
    }

    // ── Endereço (RN-026) ────────────────────────────────────────────────────

    [Fact]
    public async Task CriarAsync_ComEndereco_PersisteEnderecoDaUnidade()
    {
        _repoMock.Setup(r => r.AdicionarAsync(It.IsAny<Empresa>())).Returns(Task.CompletedTask);
        _repoMock.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var dto = CriarDto();
        dto.Endereco = new EnderecoDto
        {
            Cep = "04538-133", Logradouro = "Av. Brigadeiro Faria Lima", Numero = "3477",
            Bairro = "Itaim Bibi", Cidade = "Sao Paulo", Uf = "SP"
        };

        var resultado = await CriarServico().CriarAsync(dto);

        Assert.NotNull(resultado.Endereco);
        Assert.Equal("04538-133", resultado.Endereco!.Cep);
        Assert.Null(resultado.Endereco.Latitude);
    }
}
