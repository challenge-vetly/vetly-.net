using Moq;
using Vetly.Application.DTOs.Repasse;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Conta de repasse do prestador (§4.1, §7.3).
///
/// O produto pede no onboarding "banco, agencia, conta, CPF ou CNPJ do titular e chave
/// Pix". O que estes testes travam nao e o cadastro em si — e o sigilo: a conta e a
/// chave saem mascaradas, e o escopo e o do proprio titular, nem do Admin da unidade.
/// </summary>
public class DadosDeRepasseTests
{
    private readonly Mock<IVeterinarioRepository> _repo = new();
    private readonly Mock<ICrmvAdapter> _crmv = new();
    private readonly Mock<ISenhaHasher> _hasher = new();
    private readonly Mock<IGeradorDeSenhaTemporaria> _senha = new();
    private readonly Mock<IGeocodificacaoAdapter> _geo = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Veterinario _vet;

    public DadosDeRepasseTests()
    {
        _vet = new Veterinario("Dra. Marina", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);
        _repo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);
        _repo.Setup(r => r.Atualizar(It.IsAny<Veterinario>()));
        _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
    }

    private VeterinarioService CriarServico() =>
        new(_repo.Object, _crmv.Object, _hasher.Object, _senha.Object, _geo.Object,
            _consultaRepo.Object, _pagamentoRepo.Object, _usuario.Object);

    private static DefinirDadosDeRepasseDto Conta(
        string documento = "123.456.789-09", string conta = "0012345-6") => new()
        {
            Banco = "341",
            Agencia = "1234",
            Conta = conta,
            DocumentoTitular = documento,
            ChavePix = "marina@clinicavetly.com.br"
        };

    // ── Value object: invariantes do cadastro ────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("1234567890")]     // 10 digitos: um a menos que CPF
    [InlineData("123456789012")]   // 12 digitos: entre CPF e CNPJ
    public void Documento_ComComprimentoInvalido_ELancado(string documento)
    {
        Assert.Throws<ArgumentException>(() =>
            new DadosDeRepasse("341", "1234", "0012345-6", documento, "chave@pix.com"));
    }

    [Theory]
    [InlineData("123.456.789-09", "12345678909")]
    [InlineData("12.345.678/0001-95", "12345678000195")]
    public void Documento_EGravadoSoComDigitos(string informado, string esperado)
    {
        // O mesmo documento com e sem pontuacao viraria dois cadastros na hora de
        // bater a titularidade da conta.
        var dados = new DadosDeRepasse("341", "1234", "0012345-6", informado, "chave@pix.com");

        Assert.Equal(esperado, dados.DocumentoTitular);
    }

    [Theory]
    [InlineData("", "1234", "0012345-6", "chave@pix.com")]
    [InlineData("341", "", "0012345-6", "chave@pix.com")]
    [InlineData("341", "1234", "", "chave@pix.com")]
    [InlineData("341", "1234", "0012345-6", "")]
    public void CampoObrigatorioVazio_ELancado(
        string banco, string agencia, string conta, string pix)
    {
        // Conta pela metade nao paga ninguem, e aceita-la faria o cadastro parecer
        // completo — o pior dos dois estados.
        Assert.Throws<ArgumentException>(() =>
            new DadosDeRepasse(banco, agencia, conta, "12345678909", pix));
    }

    [Fact]
    public void ContaMascarada_MostraSoOsQuatroUltimosDigitos()
    {
        var dados = new DadosDeRepasse("341", "1234", "0012345-6", "12345678909", "chave@pix.com");

        Assert.Equal("*****45-6", dados.ContaMascarada());
    }

    [Fact]
    public void ContaCurtaDemaisParaMascarar_SaiInteiramenteCoberta()
    {
        // Expor "os tres ultimos digitos" de uma conta de tres digitos nao mascara nada.
        var dados = new DadosDeRepasse("341", "1234", "123", "12345678909", "chave@pix.com");

        Assert.Equal("***", dados.ContaMascarada());
    }

    [Fact]
    public void ChavePixMascarada_PreservaOSuficienteParaOTitularSeReconhecer()
    {
        var dados = new DadosDeRepasse("341", "1234", "0012345-6", "12345678909", "marina@vetly.com");

        Assert.EndsWith(".com", dados.ChavePixMascarada());
        Assert.DoesNotContain("marina", dados.ChavePixMascarada());
    }

    // ── Servico: escopo e resposta ───────────────────────────────────────────

    [Fact]
    public async Task SemConta_RespondeConfiguradoFalsoEmVezDe404()
    {
        // O veterinario existe; o que falta e um passo do onboarding.
        var resultado = await CriarServico().ObterDadosDeRepasseAsync();

        Assert.False(resultado.Configurado);
        Assert.Null(resultado.Banco);
        Assert.Equal(_vet.Id, resultado.TitularId);
    }

    [Fact]
    public async Task Definir_GravaEDevolveAContaMascarada()
    {
        var resultado = await CriarServico().DefinirDadosDeRepasseAsync(Conta());

        Assert.True(resultado.Configurado);
        Assert.Equal("341", resultado.Banco);
        Assert.Equal("*****45-6", resultado.Conta);
        Assert.Equal("12345678909", resultado.DocumentoTitular);

        // O numero inteiro nao pode voltar em resposta nenhuma (§7.3)
        Assert.NotEqual("0012345-6", resultado.Conta);
        Assert.DoesNotContain("marina", resultado.ChavePix!);
    }

    [Fact]
    public async Task Definir_SubstituiAContaInteira()
    {
        var servico = CriarServico();

        await servico.DefinirDadosDeRepasseAsync(Conta(conta: "0011111-1"));
        var segunda = await servico.DefinirDadosDeRepasseAsync(Conta(conta: "0022222-2"));

        // Agencia nova com conta antiga e o erro que manda dinheiro para outra pessoa
        Assert.Equal("*****22-2", segunda.Conta);
    }

    [Fact]
    public async Task Definir_ComDocumentoInvalido_VoltaComoErroDeValidacao()
    {
        // A invariante mora no value object; o servico so a traduz para o 400 da API.
        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().DefinirDadosDeRepasseAsync(Conta(documento: "123")));
    }

    [Fact]
    public async Task SemCadastroDeVeterinarioNoToken_ENegado()
    {
        // A §7.3 veda ao Admin da unidade os dados bancarios pessoais dos vinculados:
        // nao ha id na assinatura, e sem claim de veterinario nao ha o que ler.
        _usuario.SetupGet(u => u.VeterinarioId).Returns((Guid?)null);
        _usuario.SetupGet(u => u.EhAdmin).Returns(true);

        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterDadosDeRepasseAsync());
    }
}
