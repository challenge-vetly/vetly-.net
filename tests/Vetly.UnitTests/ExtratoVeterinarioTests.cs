using Moq;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.UnitTests;

/// <summary>
/// Extrato dos atendimentos do proprio veterinario (RN-024).
///
/// E a unica coisa que o profissional desativado continua alcancando, e o formato
/// segue disso: registro financeiro do proprio trabalho, sem dado de Responsavel,
/// de animal ou clinico.
/// </summary>
public class ExtratoVeterinarioTests
{
    private readonly Mock<IVeterinarioRepository> _repo = new();
    private readonly Mock<IConsultaRepository> _consultaRepo = new();
    private readonly Mock<IPagamentoRepository> _pagamentoRepo = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Veterinario _vet;

    public ExtratoVeterinarioTests()
    {
        _vet = new Veterinario("Dra. Marina Costa", new Crmv("12345-SP"), "SP",
            PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);

        _usuario.SetupGet(u => u.VeterinarioId).Returns(_vet.Id);
        _repo.Setup(r => r.ObterPorIdAsync(_vet.Id)).ReturnsAsync(_vet);
        _consultaRepo.Setup(r => r.ObterPorVeterinarioAsync(_vet.Id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync([]);
    }

    private VeterinarioService CriarServico() =>
        new(_repo.Object, Mock.Of<ICrmvAdapter>(), Mock.Of<ISenhaHasher>(),
            Mock.Of<IGeradorDeSenhaTemporaria>(), Mock.Of<IGeocodificacaoAdapter>(),
            _consultaRepo.Object, _pagamentoRepo.Object, _usuario.Object);

    /// <summary>Um atendimento realizado e cobrado, com o split ja apurado.</summary>
    private Consulta Atendimento(
        decimal valor = 200m,
        decimal comissao = 24m,
        decimal repasse = 176m,
        bool liquidado = false,
        StatusPagamento status = StatusPagamento.Confirmado,
        bool cancelada = false)
    {
        var consulta = Consulta.ParaCheckout(
            DateTime.UtcNow.AddDays(-10), _vet.Id, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());

        consulta.ConfirmarPagamento();

        if (cancelada)
            consulta.Cancelar();
        else
            consulta.Finalizar();

        var pagamento = new Pagamento(consulta.TutorId, valor, MeioPagamento.Pix, consulta.Id);

        if (status == StatusPagamento.Confirmado)
            pagamento.Confirmar();

        pagamento.RegistrarSplit(PlanoAssinatura.Profissional, 12m, comissao, repasse, _vet.Id);

        if (liquidado)
            pagamento.Liquidar();

        _pagamentoRepo.Setup(r => r.ObterPorConsultaAsync(consulta.Id)).ReturnsAsync(pagamento);

        return consulta;
    }

    private void Atendimentos(params Consulta[] consultas) =>
        _consultaRepo.Setup(r => r.ObterPorVeterinarioAsync(_vet.Id, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(consultas);

    // ── O que o extrato soma (RN-024/RN-070/RN-072) ──────────────────────────

    [Fact]
    public async Task Extrato_SomaBrutoComissaoERepasse()
    {
        Atendimentos(Atendimento(), Atendimento());

        var extrato = await CriarServico().ObterExtratoAsync(null, null);

        Assert.Equal(2, extrato.TotalDeAtendimentos);
        Assert.Equal(400m, extrato.ValorBruto);
        Assert.Equal(48m, extrato.ComissaoDaPlataforma);
        Assert.Equal(352m, extrato.RepasseTotal);
    }

    [Fact]
    public async Task Extrato_SeparaOQueJaFoiLiquidadoDoQueFaltaReceber()
    {
        Atendimentos(Atendimento(liquidado: true), Atendimento(liquidado: false));

        var extrato = await CriarServico().ObterExtratoAsync(null, null);

        // O repasse pendente e o que o profissional vem conferir
        Assert.Equal(176m, extrato.RepasseLiquidado);
        Assert.Equal(176m, extrato.RepassePendente);
    }

    [Fact]
    public async Task Extrato_ConsultaCancelada_ApareceMasNaoSomaDinheiro()
    {
        Atendimentos(
            Atendimento(),
            Atendimento(status: StatusPagamento.Pendente, cancelada: true));

        var extrato = await CriarServico().ObterExtratoAsync(null, null);

        // Aparece na lista para conferencia, mas nao soma dinheiro que nao existiu
        Assert.Equal(2, extrato.Itens.Count);
        Assert.Equal(1, extrato.TotalDeAtendimentos);
        Assert.Equal(200m, extrato.ValorBruto);
    }

    [Fact]
    public async Task Extrato_SemAtendimentos_VemVazioENaoQuebra()
    {
        var extrato = await CriarServico().ObterExtratoAsync(null, null);

        Assert.Empty(extrato.Itens);
        Assert.Equal(0m, extrato.ValorBruto);
        Assert.Equal(0m, extrato.RepassePendente);
    }

    // ── O que o extrato NÃO carrega (RN-022/RN-024) ──────────────────────────

    [Fact]
    public async Task Extrato_NaoCarregaDadoDeResponsavelAnimalNemClinico()
    {
        var consulta = Atendimento();
        Atendimentos(consulta);

        var extrato = await CriarServico().ObterExtratoAsync(null, null);

        var item = Assert.Single(extrato.Itens);

        // Identifica o atendimento pela data e pelo id da consulta, e nada mais: dado
        // clinico aqui seria dado vazando por uma porta que a RN-022 fechou
        var propriedades = item.GetType().GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("AnimalId", propriedades);
        Assert.DoesNotContain("TutorId", propriedades);
        Assert.DoesNotContain("Especie", propriedades);
        Assert.DoesNotContain("Diagnostico", propriedades);
        Assert.Equal(consulta.Id, item.ConsultaId);
    }

    // ── Escopo (RN-024/RN-105) ───────────────────────────────────────────────

    [Fact]
    public async Task Extrato_ContinuaDisponivelComOCadastroDesativado()
    {
        _vet.Desativar();
        Atendimentos(Atendimento());

        var extrato = await CriarServico().ObterExtratoAsync(null, null);

        // E exatamente o que a RN-024 garante ao profissional desligado
        Assert.False(extrato.CadastroAtivo);
        Assert.Equal(176m, extrato.RepassePendente);
    }

    [Fact]
    public async Task Extrato_SemTokenDeVeterinario_ERecusado()
    {
        _usuario.SetupGet(u => u.VeterinarioId).Returns((Guid?)null);

        var ex = await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ObterExtratoAsync(null, null));

        Assert.Equal("RN-024", ex.Codigo);
    }

    [Fact]
    public async Task Extrato_EDoProprioProfissional_SemParametroDeVeterinario()
    {
        Atendimentos(Atendimento());

        var extrato = await CriarServico().ObterExtratoAsync(null, null);

        // O escopo vem do token: nao ha como pedir o extrato de outro (RN-105)
        Assert.Equal(_vet.Id, extrato.VeterinarioId);
        Assert.Equal("12345-SP", extrato.Crmv);
    }

    // ── Período ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Extrato_SemPeriodo_UsaOsUltimosDozeMeses()
    {
        var extrato = await CriarServico().ObterExtratoAsync(null, null);

        var meses = (extrato.PeriodoFim.Year - extrato.PeriodoInicio.Year) * 12
                    + extrato.PeriodoFim.Month - extrato.PeriodoInicio.Month;

        Assert.Equal(12, meses);
    }

    [Fact]
    public async Task Extrato_ComPeriodoInvertido_NaoEAceito()
    {
        var fim = DateTime.UtcNow.AddMonths(-6);
        var inicio = DateTime.UtcNow;

        await Assert.ThrowsAsync<ValidationException>(
            () => CriarServico().ObterExtratoAsync(inicio, fim));
    }

    [Fact]
    public async Task Extrato_ComPeriodoEscolhido_RepassaAsDatasAoRepositorio()
    {
        var inicio = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fim = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

        await CriarServico().ObterExtratoAsync(inicio, fim);

        _consultaRepo.Verify(r => r.ObterPorVeterinarioAsync(_vet.Id, inicio, fim), Times.Once);
    }
}
