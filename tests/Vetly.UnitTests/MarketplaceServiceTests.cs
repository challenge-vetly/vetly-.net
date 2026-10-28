using Moq;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.DTOs.Marketplace;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.UnitTests;

/// <summary>
/// Catálogo do marketplace e validação do cupom (RN-108 a RN-111). O débito de pontos
/// em si é do <c>FidelidadeService</c>, coberto em <c>FidelidadeTests</c>.
/// </summary>
public class MarketplaceServiceTests
{
    private readonly Mock<IItemMarketplaceRepository> _itens = new();
    private readonly Mock<IFidelidadeRepository> _fidelidadeRepo = new();
    private readonly Mock<IFidelidadeService> _fidelidade = new();
    private readonly Mock<IUsuarioAtual> _usuario = new();

    private readonly Guid _tutorId = Guid.NewGuid();

    private MarketplaceService CriarServico() =>
        new(_itens.Object, _fidelidadeRepo.Object, _fidelidade.Object, _usuario.Object);

    private void ComoResponsavel(int saldo = 0)
    {
        _usuario.SetupGet(u => u.EhTutor).Returns(true);
        _usuario.SetupGet(u => u.TutorId).Returns(_tutorId);
        _fidelidade.Setup(f => f.ObterSaldoAsync(_tutorId))
            .ReturnsAsync(new SaldoDePontosDto { TutorId = _tutorId, Saldo = saldo });
    }

    private void ComoAdmin() => _usuario.SetupGet(u => u.EhAdmin).Returns(true);

    private void ComoVeterinario() => _usuario.SetupGet(u => u.EhVeterinario).Returns(true);

    private ItemMarketplace Item(int custo = 600, int? estoque = 5, bool ativo = true)
    {
        var item = new ItemMarketplace("Banho e tosa", null, CategoriaItem.Higiene, "Rede", custo, 80m, null, estoque);
        if (!ativo)
            item.Desativar();

        _itens.Setup(r => r.ObterPorIdAsync(item.Id)).ReturnsAsync(item);
        return item;
    }

    private void PaginaVazia() =>
        _itens.Setup(r => r.ObterPaginadoAsync(
                It.IsAny<FiltroItemMarketplaceDto>(), It.IsAny<bool>(), It.IsAny<int?>(),
                It.IsAny<Paginacao>(), It.IsAny<CriterioDeOrdenacao>()))
            .ReturnsAsync(new ResultadoPaginado<ItemMarketplace>([], 0, new Paginacao()));

    private static CriarItemMarketplaceDto NovoItem() => new()
    {
        Nome = "Coleira", Categoria = CategoriaItem.Higiene, Fornecedor = "Pet shop",
        CustoEmPontos = 250, ValorDeReferencia = 45.90m, Estoque = 10
    };

    // ── Listagem ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Listar_ComoResponsavel_SoItensAtivosEIgnoraIncluirInativos()
    {
        // Arrange
        ComoResponsavel();
        PaginaVazia();

        // Act
        await CriarServico().ListarAsync(new FiltroItemMarketplaceDto { IncluirInativos = true }, new Paginacao());

        // Assert
        _itens.Verify(r => r.ObterPaginadoAsync(It.IsAny<FiltroItemMarketplaceDto>(), true, null,
            It.IsAny<Paginacao>(), It.IsAny<CriterioDeOrdenacao>()), Times.Once);
    }

    [Fact]
    public async Task Listar_ComoAdminPedindoInativos_IncluiOsInativos()
    {
        // Arrange
        ComoAdmin();
        PaginaVazia();

        // Act
        await CriarServico().ListarAsync(new FiltroItemMarketplaceDto { IncluirInativos = true }, new Paginacao());

        // Assert
        _itens.Verify(r => r.ObterPaginadoAsync(It.IsAny<FiltroItemMarketplaceDto>(), false, null,
            It.IsAny<Paginacao>(), It.IsAny<CriterioDeOrdenacao>()), Times.Once);
    }

    [Fact]
    public async Task Listar_ApenasAcessiveisComoResponsavel_UsaOSaldoComoTeto()
    {
        // Arrange
        ComoResponsavel(saldo: 450);
        PaginaVazia();

        // Act
        await CriarServico().ListarAsync(new FiltroItemMarketplaceDto { ApenasAcessiveis = true }, new Paginacao());

        // Assert
        _itens.Verify(r => r.ObterPaginadoAsync(It.IsAny<FiltroItemMarketplaceDto>(), true, 450,
            It.IsAny<Paginacao>(), It.IsAny<CriterioDeOrdenacao>()), Times.Once);
    }

    [Fact]
    public async Task Listar_ApenasAcessiveisComoVeterinario_NaoLimitaNemConsultaSaldo()
    {
        // Arrange — vet nao tem pontos
        ComoVeterinario();
        PaginaVazia();

        // Act
        await CriarServico().ListarAsync(new FiltroItemMarketplaceDto { ApenasAcessiveis = true }, new Paginacao());

        // Assert
        _fidelidade.Verify(f => f.ObterSaldoAsync(It.IsAny<Guid>()), Times.Never);
        _itens.Verify(r => r.ObterPaginadoAsync(It.IsAny<FiltroItemMarketplaceDto>(), true, null,
            It.IsAny<Paginacao>(), It.IsAny<CriterioDeOrdenacao>()), Times.Once);
    }

    [Theory]
    [InlineData(null, null, "nome", false)]
    [InlineData("custoEmPontos", "desc", "custoempontos", true)]
    [InlineData("CRIADOEM", null, "criadoem", false)]
    [InlineData("fornecedor", "desc", "nome", true)]
    public async Task Listar_ResolveAOrdenacaoNaWhitelist(
        string? ordenarPor, string? direcao, string campoEsperado, bool descendenteEsperado)
    {
        // Arrange
        ComoResponsavel();
        PaginaVazia();

        // Act
        await CriarServico().ListarAsync(new FiltroItemMarketplaceDto(), new Paginacao(),
            new Ordenacao { OrdenarPor = ordenarPor, Direcao = direcao });

        // Assert
        _itens.Verify(r => r.ObterPaginadoAsync(It.IsAny<FiltroItemMarketplaceDto>(), It.IsAny<bool>(),
            It.IsAny<int?>(), It.IsAny<Paginacao>(),
            It.Is<CriterioDeOrdenacao>(c => c.Campo == campoEsperado && c.Descendente == descendenteEsperado)),
            Times.Once);
    }

    [Fact]
    public async Task Listar_MapeiaODescontoDosPontos()
    {
        // Arrange
        ComoResponsavel();
        var item = Item(custo: 600);
        _itens.Setup(r => r.ObterPaginadoAsync(It.IsAny<FiltroItemMarketplaceDto>(), It.IsAny<bool>(),
                It.IsAny<int?>(), It.IsAny<Paginacao>(), It.IsAny<CriterioDeOrdenacao>()))
            .ReturnsAsync(new ResultadoPaginado<ItemMarketplace>([item], 1, new Paginacao()));

        // Act
        var pagina = await CriarServico().ListarAsync(new FiltroItemMarketplaceDto(), new Paginacao());

        // Assert — 600 pontos = R$ 18,00 (RN-049)
        var dto = Assert.Single(pagina.Itens);
        Assert.Equal(18.00m, dto.DescontoEmReais);
        Assert.True(dto.Disponivel);
    }

    // ── Detalhe ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1000, true, 0)]
    [InlineData(600, true, 0)]
    [InlineData(450, false, 150)]
    public async Task ObterPorId_ComoResponsavel_CalculaSaldoSuficienteEQuantoFalta(
        int saldo, bool suficiente, int faltam)
    {
        // Arrange
        ComoResponsavel(saldo);
        var item = Item(custo: 600);

        // Act
        var detalhe = await CriarServico().ObterPorIdAsync(item.Id);

        // Assert
        Assert.Equal(saldo, detalhe.SaldoAtual);
        Assert.Equal(suficiente, detalhe.SaldoSuficiente);
        Assert.Equal(faltam, detalhe.PontosQueFaltam);
    }

    [Fact]
    public async Task ObterPorId_SimulaODescontoEADivisaoPelasRegrasDeFidelidade()
    {
        // Arrange — 600 pontos = R$ 18,00: faixa de R$ 10 a R$ 30, 60/40 (RN-051)
        ComoResponsavel(1000);
        var item = Item(custo: 600);

        // Act
        var detalhe = await CriarServico().ObterPorIdAsync(item.Id);

        // Assert
        Assert.Equal(FaixaDeFinanciamento.De10a30, detalhe.Faixa);
        Assert.Equal(60m, detalhe.PercentualVetly);
        Assert.Equal(40m, detalhe.PercentualPrestador);
        Assert.Equal(30, detalhe.ValidadeDoCupomEmDias);
        Assert.Equal("Simulado", detalhe.Abatimento);
    }

    [Fact]
    public async Task ObterPorId_ComoVeterinario_NaoTemSaldo()
    {
        // Arrange
        ComoVeterinario();
        var item = Item();

        // Act
        var detalhe = await CriarServico().ObterPorIdAsync(item.Id);

        // Assert — nulo, e nao zero: zero pareceria saldo vazio
        Assert.Null(detalhe.SaldoAtual);
        Assert.Null(detalhe.SaldoSuficiente);
    }

    [Fact]
    public async Task ObterPorId_ItemDesativadoParaOResponsavel_LancaNotFound()
    {
        // Arrange
        ComoResponsavel();
        var item = Item(ativo: false);

        // Act / Assert
        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().ObterPorIdAsync(item.Id));
    }

    [Fact]
    public async Task ObterPorId_ItemDesativadoParaOAdmin_Devolve()
    {
        // Arrange
        ComoAdmin();
        var item = Item(ativo: false);

        // Act
        var detalhe = await CriarServico().ObterPorIdAsync(item.Id);

        // Assert
        Assert.False(detalhe.Ativo);
    }

    // ── Manutenção do catálogo ───────────────────────────────────────────────

    [Fact]
    public async Task Criar_ComoAdmin_GravaItemAtivo()
    {
        // Arrange
        ComoAdmin();
        ItemMarketplace? gravado = null;
        _itens.Setup(r => r.AdicionarAsync(It.IsAny<ItemMarketplace>()))
            .Callback<ItemMarketplace>(i => gravado = i).Returns(Task.CompletedTask);

        // Act
        var dto = await CriarServico().CriarAsync(NovoItem());

        // Assert
        Assert.NotNull(gravado);
        Assert.True(dto.Ativo);
        Assert.Equal(250, dto.CustoEmPontos);
        _itens.Verify(r => r.SalvarAsync(), Times.Once);
    }

    [Fact]
    public async Task Criar_ComoResponsavel_LancaAcessoNegado()
    {
        // Arrange
        ComoResponsavel();

        // Act / Assert
        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().CriarAsync(NovoItem()));
        _itens.Verify(r => r.AdicionarAsync(It.IsAny<ItemMarketplace>()), Times.Never);
    }

    [Fact]
    public async Task Criar_SemCategoria_LancaValidation()
    {
        // Arrange
        ComoAdmin();
        var dto = NovoItem();
        dto.Categoria = null;

        // Act / Assert
        await Assert.ThrowsAsync<ValidationException>(() => CriarServico().CriarAsync(dto));
    }

    [Fact]
    public async Task Atualizar_ComAtivoTrue_ReativaOItem()
    {
        // Arrange
        ComoAdmin();
        var item = Item(ativo: false);
        var dto = new AtualizarItemMarketplaceDto
        {
            Nome = "Banho", Categoria = CategoriaItem.Higiene, Fornecedor = "Rede",
            CustoEmPontos = 700, ValorDeReferencia = 90m, Ativo = true
        };

        // Act
        var atualizado = await CriarServico().AtualizarAsync(item.Id, dto);

        // Assert
        Assert.True(atualizado.Ativo);
        Assert.Equal(700, atualizado.CustoEmPontos);
    }

    [Fact]
    public async Task Atualizar_ComAtivoFalse_Desativa()
    {
        // Arrange
        ComoAdmin();
        var item = Item();
        var dto = new AtualizarItemMarketplaceDto
        {
            Nome = "Banho", Categoria = CategoriaItem.Higiene, Fornecedor = "Rede",
            CustoEmPontos = 600, ValorDeReferencia = 80m, Ativo = false
        };

        // Act
        var atualizado = await CriarServico().AtualizarAsync(item.Id, dto);

        // Assert
        Assert.False(atualizado.Ativo);
    }

    [Fact]
    public async Task Atualizar_ItemInexistente_LancaNotFound()
    {
        // Arrange
        ComoAdmin();

        // Act / Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => CriarServico().AtualizarAsync(Guid.NewGuid(), new AtualizarItemMarketplaceDto
            {
                Nome = "X", Categoria = CategoriaItem.Higiene, Fornecedor = "Y", CustoEmPontos = 1
            }));
    }

    [Fact]
    public async Task Desativar_ComoAdmin_DesativaSemApagar()
    {
        // Arrange
        ComoAdmin();
        var item = Item();

        // Act
        await CriarServico().DesativarAsync(item.Id);

        // Assert
        Assert.False(item.Ativo);
        _itens.Verify(r => r.Remover(It.IsAny<ItemMarketplace>()), Times.Never);
        _itens.Verify(r => r.SalvarAsync(), Times.Once);
    }

    [Fact]
    public async Task Desativar_ItemInexistente_LancaNotFound()
    {
        // Arrange
        ComoAdmin();

        // Act / Assert
        await Assert.ThrowsAsync<NotFoundException>(() => CriarServico().DesativarAsync(Guid.NewGuid()));
    }

    // ── Resgate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resgatar_ComoResponsavel_DelegaSoComOIdDoItem()
    {
        // Arrange
        ComoResponsavel();
        var itemId = Guid.NewGuid();
        _fidelidade.Setup(f => f.ResgatarAsync(_tutorId, It.IsAny<SimularResgateDto>()))
            .ReturnsAsync(new CupomDto());

        // Act
        await CriarServico().ResgatarAsync(new ResgatarItemDto { ItemId = itemId });

        // Assert — nenhum campo da forma legada vai junto
        _fidelidade.Verify(f => f.ResgatarAsync(_tutorId, It.Is<SimularResgateDto>(d =>
            d.ItemId == itemId && d.Pontos == 0 && d.ItemRef == string.Empty)), Times.Once);
    }

    [Fact]
    public async Task Resgatar_SemTutorNoToken_LancaAcessoNegado()
    {
        // Arrange
        ComoVeterinario();

        // Act / Assert
        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ResgatarAsync(new ResgatarItemDto { ItemId = Guid.NewGuid() }));
    }

    // ── Validação do cupom (RN-110) ──────────────────────────────────────────

    private CupomResgate Cupom(ItemMarketplace? item = null, DateTime? expiraEm = null)
    {
        var cupom = new CupomResgate(_tutorId, "ref", "Banho", CategoriaItem.Higiene, 600, 18m, item?.Id);
        if (expiraEm is { } vencimento)
            typeof(CupomResgate).GetProperty(nameof(CupomResgate.ExpiraEm))!.SetValue(cupom, vencimento);

        _fidelidadeRepo.Setup(r => r.ObterCupomPorCodigoAsync(cupom.CodigoQr)).ReturnsAsync(cupom);
        return cupom;
    }

    [Fact]
    public async Task ValidarCupom_Vigente_MarcaComoResgatado()
    {
        // Arrange
        ComoVeterinario();
        var cupom = Cupom();

        // Act — codigo digitado em minusculas e com espaco
        var dto = await CriarServico().ValidarCupomAsync(new ValidarCupomDto { CodigoQr = $" {cupom.CodigoQr.ToLowerInvariant()} " });

        // Assert
        Assert.Equal(StatusCupom.Resgatado, dto.Status);
        Assert.NotNull(dto.ResgatadoEm);
        _fidelidadeRepo.Verify(r => r.SalvarAsync(), Times.Once);
    }

    [Fact]
    public async Task ValidarCupom_JaUtilizado_LancaConflitoMARKETPLACE001()
    {
        // Arrange
        ComoAdmin();
        var cupom = Cupom();
        cupom.Resgatar(DateTime.UtcNow);

        // Act
        var erro = await Assert.ThrowsAsync<ConflitoDeEstadoException>(
            () => CriarServico().ValidarCupomAsync(new ValidarCupomDto { CodigoQr = cupom.CodigoQr }));

        // Assert
        Assert.Equal("MARKETPLACE-001", erro.Codigo);
    }

    [Fact]
    public async Task ValidarCupom_Vencido_LancaRN053EDevolveAUnidade()
    {
        // Arrange
        ComoVeterinario();
        var item = Item(estoque: 2);
        var cupom = Cupom(item, expiraEm: DateTime.UtcNow.AddMinutes(-1));

        // Act
        var erro = await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ValidarCupomAsync(new ValidarCupomDto { CodigoQr = cupom.CodigoQr }));

        // Assert
        Assert.Equal("RN-053", erro.Codigo);
        Assert.Equal(StatusCupom.Expirado, cupom.Status);
        Assert.Equal(3, item.Estoque);
        _fidelidadeRepo.Verify(r => r.SalvarAsync(), Times.Once);
    }

    [Fact]
    public async Task ValidarCupom_JaExpiradoPelaRotina_NaoDevolveDeNovo()
    {
        // Arrange — a rotina diaria ja expirou e devolveu a unidade
        ComoVeterinario();
        var item = Item(estoque: 2);
        var cupom = Cupom(item, expiraEm: DateTime.UtcNow.AddDays(-1));
        cupom.Expirar();

        // Act
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => CriarServico().ValidarCupomAsync(new ValidarCupomDto { CodigoQr = cupom.CodigoQr }));

        // Assert
        Assert.Equal(2, item.Estoque);
        _fidelidadeRepo.Verify(r => r.SalvarAsync(), Times.Never);
    }

    [Fact]
    public async Task ValidarCupom_CodigoInexistente_LancaNotFound()
    {
        // Arrange
        ComoVeterinario();

        // Act / Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => CriarServico().ValidarCupomAsync(new ValidarCupomDto { CodigoQr = "VETLY-NAOEXISTE00" }));
    }

    [Fact]
    public async Task ValidarCupom_ComoResponsavel_LancaAcessoNegado()
    {
        // Arrange — o Responsavel nao valida o proprio cupom
        ComoResponsavel();

        // Act / Assert
        await Assert.ThrowsAsync<AcessoNegadoException>(
            () => CriarServico().ValidarCupomAsync(new ValidarCupomDto { CodigoQr = "VETLY-X" }));
    }

    // ── Taxas de listagem (RN-111) ───────────────────────────────────────────

    [Fact]
    public async Task ObterTaxas_ComoAdmin_TrazAsTresCategoriasComItensAtivos()
    {
        // Arrange
        ComoAdmin();
        _itens.Setup(r => r.ContarAtivosPorCategoriaAsync())
            .ReturnsAsync(new Dictionary<CategoriaItem, int> { [CategoriaItem.Higiene] = 3 });

        // Act
        var taxas = await CriarServico().ObterTaxasDeListagemAsync();

        // Assert
        Assert.Equal(3, taxas.Count);
        Assert.All(taxas, t => Assert.False(t.Cobrada));
        Assert.Equal(3, taxas.Single(t => t.Categoria == CategoriaItem.Higiene).ItensAtivos);
        Assert.Equal(0, taxas.Single(t => t.Categoria == CategoriaItem.Alimentacao).ItensAtivos);
        Assert.Equal(250m, taxas.Single(t => t.Categoria == CategoriaItem.Medicamentos).Valor);
    }

    [Fact]
    public async Task ObterTaxas_ComoResponsavel_LancaAcessoNegado()
    {
        // Arrange
        ComoResponsavel();

        // Act / Assert
        await Assert.ThrowsAsync<AcessoNegadoException>(() => CriarServico().ObterTaxasDeListagemAsync());
    }
}
