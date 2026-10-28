using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.DTOs.Marketplace;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>
/// Catálogo do marketplace e o ciclo do cupom (RN-108 a RN-111).
///
/// <para>
/// O resgate em si continua no <see cref="FidelidadeService"/>, que é dono do saldo,
/// do consumo FIFO e da emissão do cupom: aqui só se escolhe o item. Duplicar o
/// débito de pontos em dois serviços seria ter duas regras de FIFO para manter
/// iguais — e o dia em que divergirem, o saldo diverge.
/// </para>
/// </summary>
public class MarketplaceService : IMarketplaceService
{
    private readonly IItemMarketplaceRepository _itens;
    private readonly IFidelidadeRepository _fidelidadeRepo;
    private readonly IFidelidadeService _fidelidade;
    private readonly IUsuarioAtual _usuario;

    public MarketplaceService(
        IItemMarketplaceRepository itens,
        IFidelidadeRepository fidelidadeRepo,
        IFidelidadeService fidelidade,
        IUsuarioAtual usuario)
    {
        _itens = itens;
        _fidelidadeRepo = fidelidadeRepo;
        _fidelidade = fidelidade;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<ResultadoPaginado<ItemMarketplaceDto>> ListarAsync(
        FiltroItemMarketplaceDto filtro, Paginacao paginacao, Ordenacao? ordenacao = null)
    {
        var criterio = (ordenacao ?? new Ordenacao()).Resolver(
            CamposDeOrdenacao.Marketplace.Todos, CamposDeOrdenacao.Marketplace.Nome, descendentePadrao: false);

        // Inativo e manutencao do catalogo: so o Admin enxerga, e so se pedir
        var apenasAtivos = !(_usuario.EhAdmin && filtro.IncluirInativos);

        // "So o que consigo resgatar" usa o saldo calculado aqui, nunca um numero
        // vindo do cliente. Sem pontos (vet, Admin), nao ha contra o que filtrar.
        int? teto = null;
        if (filtro.ApenasAcessiveis && _usuario.EhTutor && _usuario.TutorId is { } tutorId)
            teto = (await _fidelidade.ObterSaldoAsync(tutorId)).Saldo;

        var pagina = await _itens.ObterPaginadoAsync(filtro, apenasAtivos, teto, paginacao, criterio);
        return pagina.Mapear(Mapear);
    }

    /// <inheritdoc/>
    public async Task<ItemMarketplaceDetalheDto> ObterPorIdAsync(Guid id)
    {
        var item = await _itens.ObterPorIdAsync(id);

        // Item desativado nao existe para quem nao mantem o catalogo
        if (item is null || (!item.Ativo && !_usuario.EhAdmin))
            throw new NotFoundException("ItemMarketplace", id);

        var desconto = RegrasDeFidelidade.EmReais(item.CustoEmPontos);
        var faixa = RegrasDeFidelidade.FaixaPara(desconto);
        var percentualVetly = RegrasDeFidelidade.PercentualVetlyDe(faixa);

        var detalhe = new ItemMarketplaceDetalheDto
        {
            Faixa = faixa,
            PercentualVetly = percentualVetly,
            PercentualPrestador = 100m - percentualVetly,
            ValidadeDoCupomEmDias = (int)RegrasDeFidelidade.ValidadeDoCupom.TotalDays
        };
        Preencher(detalhe, item);

        if (_usuario.EhTutor && _usuario.TutorId is { } tutorId)
        {
            var saldo = (await _fidelidade.ObterSaldoAsync(tutorId)).Saldo;
            detalhe.SaldoAtual = saldo;
            detalhe.SaldoSuficiente = saldo >= item.CustoEmPontos;
            detalhe.PontosQueFaltam = Math.Max(0, item.CustoEmPontos - saldo);
        }

        return detalhe;
    }

    /// <inheritdoc/>
    public async Task<ItemMarketplaceDto> CriarAsync(CriarItemMarketplaceDto dto)
    {
        GarantirAdmin();

        var item = new ItemMarketplace(
            dto.Nome, dto.Descricao, CategoriaDe(dto), dto.Fornecedor,
            dto.CustoEmPontos, dto.ValorDeReferencia, dto.ImagemMidiaId, dto.Estoque);

        await _itens.AdicionarAsync(item);
        await _itens.SalvarAsync();

        return Mapear(item);
    }

    /// <inheritdoc/>
    public async Task<ItemMarketplaceDto> AtualizarAsync(Guid id, AtualizarItemMarketplaceDto dto)
    {
        GarantirAdmin();

        var item = await _itens.ObterPorIdAsync(id)
            ?? throw new NotFoundException("ItemMarketplace", id);

        item.Atualizar(
            dto.Nome, dto.Descricao, CategoriaDe(dto), dto.Fornecedor,
            dto.CustoEmPontos, dto.ValorDeReferencia, dto.ImagemMidiaId, dto.Estoque);

        if (dto.Ativo == true)
            item.Reativar();
        else if (dto.Ativo == false)
            item.Desativar();

        _itens.Atualizar(item);
        await _itens.SalvarAsync();

        return Mapear(item);
    }

    /// <inheritdoc/>
    public async Task DesativarAsync(Guid id)
    {
        GarantirAdmin();

        var item = await _itens.ObterPorIdAsync(id)
            ?? throw new NotFoundException("ItemMarketplace", id);

        // Desativa, nao apaga: os cupons ja emitidos apontam para o item
        item.Desativar();
        _itens.Atualizar(item);
        await _itens.SalvarAsync();
    }

    /// <inheritdoc/>
    public Task<CupomDto> ResgatarAsync(ResgatarItemDto dto)
    {
        var tutorId = _usuario.TutorId
            ?? throw new AcessoNegadoException("RN-106",
                "O resgate e do Responsavel. Entre com um cadastro de Responsavel.");

        // So o id viaja: nome, categoria e custo saem do catalogo (RN-108)
        return _fidelidade.ResgatarAsync(tutorId, new SimularResgateDto { ItemId = dto.ItemId });
    }

    /// <inheritdoc/>
    public async Task<CupomDto> ValidarCupomAsync(ValidarCupomDto dto)
    {
        if (!_usuario.EhVeterinario && !_usuario.EhAdmin)
            throw new AcessoNegadoException("RN-110",
                "A validacao do cupom e feita pelo estabelecimento.");

        // O codigo e gerado em maiusculas; digitado a mao, pode chegar de qualquer jeito
        var codigo = dto.CodigoQr.Trim().ToUpperInvariant();

        var cupom = await _fidelidadeRepo.ObterCupomPorCodigoAsync(codigo)
            ?? throw new NotFoundException("Cupom nao encontrado para o codigo informado.");

        var agora = DateTime.UtcNow;

        if (cupom.Status == StatusCupom.Resgatado)
            throw new ConflitoDeEstadoException("MARKETPLACE-001",
                $"Este cupom ja foi utilizado em {cupom.ResgatadoEm:dd/MM/yyyy HH:mm} (UTC).");

        if (!cupom.Vigente(agora))
        {
            // A rotina diaria pode nao ter passado ainda: o cupom vence aqui, e a
            // unidade volta ao estoque nesta mesma gravacao (RN-109)
            if (cupom.Expirar())
            {
                await DevolverUnidadeAsync(cupom);
                _fidelidadeRepo.AtualizarCupom(cupom);
                await _fidelidadeRepo.SalvarAsync();
            }

            throw new BusinessRuleException("RN-053",
                $"Cupom vencido em {cupom.ExpiraEm:dd/MM/yyyy}. Os pontos nao retornam ao saldo.");
        }

        cupom.Resgatar(agora);
        _fidelidadeRepo.AtualizarCupom(cupom);
        await _fidelidadeRepo.SalvarAsync();

        return FidelidadeService.Mapear(cupom);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TaxaDeListagemDto>> ObterTaxasDeListagemAsync()
    {
        GarantirAdmin();

        var ativos = await _itens.ContarAtivosPorCategoriaAsync();

        return Enum.GetValues<CategoriaItem>()
            .Select(categoria => new TaxaDeListagemDto
            {
                Categoria = categoria,
                Valor = TaxaDeListagem.Para(categoria),

                // RN-111: registrada para o modelo de receita ser visivel, sem cobranca
                Cobrada = false,
                ItensAtivos = ativos.GetValueOrDefault(categoria)
            })
            .ToList();
    }

    private async Task DevolverUnidadeAsync(CupomResgate cupom)
    {
        if (cupom.ItemMarketplaceId is not { } itemId)
            return;

        if (await _itens.ObterPorIdAsync(itemId) is { } item)
        {
            item.DevolverUnidade();
            _itens.Atualizar(item);
        }
    }

    /// <summary>
    /// Defesa em profundidade: a policy <c>ApenasAdmin</c> já barra no controller, e
    /// isto barra quem chamar o serviço por outro caminho.
    /// </summary>
    private void GarantirAdmin()
    {
        if (!_usuario.EhAdmin)
            throw new AcessoNegadoException("RN-106", "Somente o Admin mantem o catalogo do marketplace.");
    }

    /// <summary>O DTO marca a categoria como obrigatória; aqui só se desembrulha.</summary>
    private static CategoriaItem CategoriaDe(CriarItemMarketplaceDto dto) =>
        dto.Categoria ?? throw new ValidationException("categoria", "A categoria e obrigatoria.");

    private static ItemMarketplaceDto Mapear(ItemMarketplace item)
    {
        var dto = new ItemMarketplaceDto();
        Preencher(dto, item);
        return dto;
    }

    private static void Preencher(ItemMarketplaceDto dto, ItemMarketplace item)
    {
        dto.Id = item.Id;
        dto.Nome = item.Nome;
        dto.Descricao = item.Descricao;
        dto.Categoria = item.Categoria;
        dto.Fornecedor = item.Fornecedor;
        dto.CustoEmPontos = item.CustoEmPontos;
        dto.DescontoEmReais = RegrasDeFidelidade.EmReais(item.CustoEmPontos);
        dto.ValorDeReferencia = item.ValorDeReferencia;
        dto.ImagemMidiaId = item.ImagemMidiaId;
        dto.Estoque = item.Estoque;
        dto.Disponivel = item.Disponivel;
        dto.Ativo = item.Ativo;
        dto.CriadoEm = item.CriadoEm;
        dto.AtualizadoEm = item.AtualizadoEm;
    }
}
