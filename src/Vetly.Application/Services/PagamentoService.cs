using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Application.Strategies.Split;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>Servico de pagamentos. Processa criacao e split financeiro via Strategy.</summary>
public class PagamentoService : IPagamentoService
{
    private readonly IPagamentoRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IEmpresaRepository _empresaRepo;
    private readonly IEnumerable<ISplitFinanceiroStrategy> _splitStrategies;
    private readonly IUsuarioAtual _usuario;

    public PagamentoService(
        IPagamentoRepository repo,
        IVeterinarioRepository vetRepo,
        IConsultaRepository consultaRepo,
        IEmpresaRepository empresaRepo,
        IEnumerable<ISplitFinanceiroStrategy> splitStrategies,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _consultaRepo = consultaRepo;
        _empresaRepo = empresaRepo;
        _splitStrategies = splitStrategies;
        _usuario = usuario;
    }

    /// <summary>
    /// Lista pagamentos no escopo de quem chama: o Responsável vê a própria carteira,
    /// o Admin vê o consolidado (RN-106).
    /// </summary>
    public async Task<ResultadoPaginado<PagamentoDto>> ObterTodosAsync(Paginacao paginacao)
    {
        // Escopo do token, nunca parametro do cliente. Sem escopo reconhecido, nada.
        Guid? tutorId = _usuario.EhAdmin ? null : (_usuario.TutorId ?? Guid.Empty);

        var pagina = await _repo.ObterPaginadoAsync(paginacao, tutorId);
        return pagina.Mapear(MapearParaDto);
    }

    public async Task<PagamentoDto> ObterPorIdAsync(Guid id)
    {
        var pagamento = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Pagamento", id);

        if (!_usuario.EhAdmin && _usuario.TutorId != pagamento.TutorId)
            throw new AcessoNegadoException("RN-106", "Este pagamento nao pertence ao seu escopo de acesso.");

        return MapearParaDto(pagamento);
    }

    public async Task<PagamentoDto> CriarAsync(CriarPagamentoDto dto)
    {
        var pagamento = new Pagamento(dto.TutorId, dto.Valor, dto.MeioPagamento, dto.ConsultaId, dto.InternacaoId);
        pagamento.Confirmar(); // pagamento confirmado ao criar (integracao com gateway real seria assincrona)
        await _repo.AdicionarAsync(pagamento);
        await _repo.SalvarAsync();
        return MapearParaDto(pagamento);
    }

    /// <summary>
    /// Apura a reparticao da transacao pelo PLANO do prestador (RN-070).
    ///
    /// Ate aqui o criterio era a persona (autonomo 80 / vinculado 60), o que contradizia
    /// a decisao fechada de produto — conflito C-01. O Strategy Pattern permanece; o que
    /// mudou foi o criterio e o fato de haver um unico repasse (RN-072).
    /// </summary>
    public async Task<PagamentoDto> ProcessarSplitAsync(Guid id)
    {
        var pagamento = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Pagamento", id);

        // Recupera o veterinario via consulta vinculada
        if (pagamento.ConsultaId is null)
            throw new BusinessRuleException("PAGAMENTO-001", "Split so pode ser processado para pagamentos vinculados a consultas.");

        var consulta = await _consultaRepo.ObterPorIdAsync(pagamento.ConsultaId.Value)
            ?? throw new NotFoundException("Consulta", pagamento.ConsultaId.Value);

        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", consulta.VeterinarioId);

        var (plano, destinatarioId) = await ResolverPlanoEDestinatarioAsync(vet);

        var strategy = _splitStrategies.FirstOrDefault(s => s.Aplicavel(plano))
            ?? throw new BusinessRuleException("RN-070", $"Nao ha regra de split para o plano {plano}.");

        var split = strategy.Calcular(pagamento.Valor);

        pagamento.RegistrarSplit(split.Plano, split.TakeRate, split.Comissao, split.Repasse, destinatarioId);
        _repo.Atualizar(pagamento);
        await _repo.SalvarAsync();
        return MapearParaDto(pagamento);
    }

    /// <summary>
    /// Descobre o plano que rege a transacao e quem recebe o repasse (RN-070/RN-072).
    ///
    /// Vet vinculado: vale o plano da CLINICA, que e quem assina — o Enterprise, por
    /// exemplo, e precificado por numero de vets da unidade. O repasse vai a ela, e a
    /// remuneracao interna do profissional fica fora do escopo da plataforma.
    /// Vet autonomo: o proprio plano, e o repasse vai direto a ele.
    /// </summary>
    private async Task<(PlanoAssinatura Plano, Guid DestinatarioId)> ResolverPlanoEDestinatarioAsync(Veterinario vet)
    {
        if (vet.EmpresaId is not { } empresaId)
            return (vet.Plano, vet.Id);

        var empresa = await _empresaRepo.ObterPorIdAsync(empresaId);

        return empresa is null
            ? (vet.Plano, vet.Id)
            : (empresa.Plano, empresa.Id);
    }

    private static PagamentoDto MapearParaDto(Pagamento p) => new()
    {
        Id = p.Id, TutorId = p.TutorId, ConsultaId = p.ConsultaId, InternacaoId = p.InternacaoId,
        Valor = p.Valor, MeioPagamento = p.MeioPagamento, Momento = p.Momento,
        StatusPagamento = p.StatusPagamento, PercentualSplit = p.PercentualSplit,
        ValorEstornado = p.ValorEstornado,
        PlanoAplicado = p.PlanoAplicado, TakeRate = p.TakeRate,
        Comissao = p.Comissao, Repasse = p.Repasse,
        DestinatarioRepasseId = p.DestinatarioRepasseId
    };
}
