using Vetly.Application.DTOs.Financeiro;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Consolidado financeiro e liquidação de repasses (RN-070/RN-071/RN-072).
///
/// A conta que este serviço precisa fechar é uma só: <c>bruto = comissão + repasse +
/// desconto</c>. Se os três não somam o bruto, há dinheiro sem dono — e o painel diz
/// isso em vez de escondê-lo atrás de um total bonito.
/// </summary>
public class FinanceiroService : IFinanceiroService
{
    private readonly IPagamentoRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IEmpresaRepository _empresaRepo;
    private readonly IUsuarioAtual _usuario;

    public FinanceiroService(
        IPagamentoRepository repo,
        IVeterinarioRepository vetRepo,
        IEmpresaRepository empresaRepo,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _empresaRepo = empresaRepo;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<ConsolidadoFinanceiroDto> ObterConsolidadoAsync(DateTime? inicio, DateTime? fim)
    {
        GarantirAdmin();

        var (de, ate) = NormalizarPeriodo(inicio, fim);

        var pagamentos = (await _repo.ObterConfirmadosNoPeriodoAsync(de, ate)).ToList();

        var consolidado = new ConsolidadoFinanceiroDto
        {
            PeriodoInicio = de,
            PeriodoFim = ate,
            TotalDeTransacoes = pagamentos.Count,
            ValorBruto = pagamentos.Sum(p => p.Valor),
            ComissaoLiquida = pagamentos.Sum(p => p.Comissao ?? 0m),
            DescontosDeFidelidade = pagamentos.Sum(p => p.ValorDoDesconto ?? 0m),
            RepasseTotal = pagamentos.Sum(p => p.Repasse ?? 0m),
            RepasseLiquidado = pagamentos.Where(p => p.Liquidado).Sum(p => p.Repasse ?? 0m),
            RepassePendente = pagamentos.Where(p => !p.Liquidado).Sum(p => p.Repasse ?? 0m)
        };

        // A verificação é explícita porque split incoerente é silencioso: os totais
        // continuam somando, e só a conta cruzada revela o problema.
        consolidado.Fecha =
            consolidado.ComissaoLiquida + consolidado.RepasseTotal + consolidado.DescontosDeFidelidade
            == consolidado.ValorBruto;

        consolidado.PorDestinatario = await MontarPorDestinatarioAsync(pagamentos);

        return consolidado;
    }

    /// <inheritdoc/>
    public async Task<LiquidacaoRealizadaDto> LiquidarAsync(LiquidarRepasseDto dto)
    {
        GarantirAdmin();

        if (dto.Inicio > dto.Fim)
            throw new ValidationException("inicio", "O inicio do periodo nao pode ser depois do fim.");

        var pagamentos = (await _repo.ObterConfirmadosNoPeriodoAsync(dto.Inicio, dto.Fim))
            .Where(p => dto.DestinatarioId is not { } destinatario || p.DestinatarioRepasseId == destinatario)
            .Where(p => p.Repasse is > 0m)
            .ToList();

        // Já liquidado é ignorado, não recontado: chamar o mesmo fechamento duas vezes
        // não pode pagar duas vezes, e a operação repete fechamento com frequência.
        var jaLiquidados = pagamentos.Count(p => p.Liquidado);
        var aLiquidar = pagamentos.Where(p => !p.Liquidado).ToList();

        foreach (var pagamento in aLiquidar)
        {
            pagamento.Liquidar();
            _repo.Atualizar(pagamento);
        }

        if (aLiquidar.Count > 0)
            await _repo.SalvarAsync();

        return new LiquidacaoRealizadaDto
        {
            PeriodoInicio = dto.Inicio,
            PeriodoFim = dto.Fim,
            DestinatarioId = dto.DestinatarioId,
            Referencia = dto.Referencia,
            PagamentosLiquidados = aLiquidar.Count,
            ValorLiquidado = aLiquidar.Sum(p => p.Repasse ?? 0m),
            JaEstavamLiquidados = jaLiquidados,
            RealizadaEm = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Consolida o repasse por destinatário (RN-072). É a lista que a operação usa
    /// para pagar: um prestador, um valor.
    /// </summary>
    private async Task<List<RepassePorDestinatarioDto>> MontarPorDestinatarioAsync(List<Pagamento> pagamentos)
    {
        var porDestinatario = new List<RepassePorDestinatarioDto>();

        foreach (var grupo in pagamentos
                     .Where(p => p.DestinatarioRepasseId is not null)
                     .GroupBy(p => p.DestinatarioRepasseId!.Value))
        {
            porDestinatario.Add(new RepassePorDestinatarioDto
            {
                DestinatarioId = grupo.Key,
                Nome = await ResolverNomeAsync(grupo.Key),
                TotalDeAtendimentos = grupo.Count(),
                RepasseTotal = grupo.Sum(p => p.Repasse ?? 0m),
                RepasseLiquidado = grupo.Where(p => p.Liquidado).Sum(p => p.Repasse ?? 0m),
                RepassePendente = grupo.Where(p => !p.Liquidado).Sum(p => p.Repasse ?? 0m)
            });
        }

        // Maior pendência primeiro: é a ordem em que a operação resolve a fila
        return [.. porDestinatario.OrderByDescending(d => d.RepassePendente)];
    }

    /// <summary>
    /// O destinatário é um veterinário autônomo ou uma clínica (RN-072) — não há campo
    /// que diga qual, então se procura nos dois.
    /// </summary>
    private async Task<string?> ResolverNomeAsync(Guid destinatarioId)
    {
        var vet = await _vetRepo.ObterPorIdAsync(destinatarioId);

        if (vet is not null)
            return vet.Nome;

        var empresa = await _empresaRepo.ObterPorIdAsync(destinatarioId);

        return empresa?.Nome;
    }

    /// <summary>
    /// Período padrão: o mês corrente. É o recorte do fechamento, e evita que a
    /// chamada sem parâmetro varra a base inteira.
    /// </summary>
    private static (DateTime, DateTime) NormalizarPeriodo(DateTime? inicio, DateTime? fim)
    {
        var agora = DateTime.UtcNow;

        var de = inicio ?? new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var ate = fim ?? de.AddMonths(1).AddTicks(-1);

        if (de > ate)
            throw new ValidationException("inicio", "O inicio do periodo nao pode ser depois do fim.");

        return (de, ate);
    }

    /// <summary>
    /// O consolidado é da plataforma inteira: só o Admin alcança (RN-106). Um
    /// veterinário vê o próprio dinheiro pelo extrato (RN-024).
    /// </summary>
    private void GarantirAdmin()
    {
        if (!_usuario.EhAdmin)
            throw new AcessoNegadoException("RN-106",
                "O consolidado financeiro e restrito a administracao.");
    }
}
