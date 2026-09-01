using System.Text.Json;
using Vetly.Application.DTOs.Internacao;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de internações: abertura com caução, apuração diária e alta com saldo
/// (RN-100 a RN-102).
///
/// A internação é a exceção declarada ao pagamento antecipado (RN-101): cobra caução
/// na entrada e o saldo na saída. Os dois passam pelo mesmo adaptador de pagamento
/// das consultas — não há um caminho paralelo de dinheiro, porque um caminho paralelo
/// é o que faz a conferência financeira não fechar.
/// </summary>
public class InternacaoService : IInternacaoService
{
    private readonly IInternacaoRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IPagamentoService _pagamentos;
    private readonly INotificacaoService _notificacoes;
    private readonly IUsuarioAtual _usuario;

    public InternacaoService(
        IInternacaoRepository repo,
        IAnimalRepository animalRepo,
        IPagamentoService pagamentos,
        INotificacaoService notificacoes,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _pagamentos = pagamentos;
        _notificacoes = notificacoes;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<InternacaoDto>> ObterTodosAsync()
    {
        if (_usuario.EhAdmin)
            return (await _repo.ObterTodosAsync()).Select(MapearParaDto);

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId)
            return (await _repo.ObterTodosAsync())
                .Where(i => i.VeterinarioId == vetId)
                .Select(MapearParaDto);

        if (_usuario.EhTutor && _usuario.TutorId is { } tutorId)
        {
            var animais = await _animalRepo.ObterPorTutorAsync(tutorId);
            var internacoes = new List<Internacao>();

            foreach (var animal in animais)
                internacoes.AddRange(await _repo.ObterPorAnimalAsync(animal.Id));

            return internacoes.Select(MapearParaDto);
        }

        // Falha fechado: dado de saúde é sensível (RN-069)
        return [];
    }

    /// <inheritdoc/>
    public async Task<InternacaoDto> ObterPorIdAsync(Guid id)
    {
        var internacao = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Internacao", id);

        await GarantirLeituraAsync(internacao);

        return MapearParaDto(internacao);
    }

    /// <inheritdoc/>
    public async Task<InternacaoDto> AbrirAsync(CriarInternacaoDto dto)
    {
        var veterinarioId = ResolverResponsavel(dto.VeterinarioId);

        var animal = await _animalRepo.ObterPorIdAsync(dto.AnimalId)
            ?? throw new NotFoundException("Animal", dto.AnimalId);

        // Duas internações ativas sobre o mesmo animal fariam duas apurações paralelas
        // do mesmo período, e nenhuma delas estaria certa.
        var ativa = await _repo.ObterAtivaDoAnimalAsync(dto.AnimalId);

        if (ativa is not null)
            throw new BusinessRuleException("INTERNACAO-001",
                "O animal ja possui uma internacao ativa. Finalize a atual antes de abrir outra.");

        var internacao = new Internacao(dto.AnimalId, veterinarioId, dto.ValorCaucao);

        await _repo.AdicionarAsync(internacao);
        await _repo.SalvarAsync();

        // RN-101: a caução é cobrança de verdade, pelo mesmo adaptador das consultas.
        // Registrá-la só na entidade deixaria dinheiro fora do consolidado financeiro.
        if (dto.ValorCaucao > 0)
        {
            await _pagamentos.CriarCobrancaAsync(new CriarPagamentoDto
            {
                TutorId = animal.TutorId,
                InternacaoId = internacao.Id,
                Valor = dto.ValorCaucao,
                MeioPagamento = dto.MeioPagamento
            });
        }

        return MapearParaDto(internacao);
    }

    /// <inheritdoc/>
    public async Task<InternacaoDto> RegistrarProcedimentosAsync(Guid id, RegistrarProcedimentosDto dto)
    {
        var internacao = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Internacao", id);

        GarantirEscritaDoResponsavel(internacao);

        if (!internacao.EstaAtiva())
            throw new BusinessRuleException("INTERNACAO-002",
                "Nao e possivel registrar procedimentos em internacao ja encerrada.");

        var json = JsonSerializer.Serialize(dto.Procedimentos);

        internacao.RegistrarProcedimentoDiario(json);
        internacao.ApurarValor(dto.Procedimentos.Sum(p => p.Valor)); // RN-100

        _repo.Atualizar(internacao);
        await _repo.SalvarAsync();

        // RN-100: quem tem um animal internado quer saber do dia dele. A ausência de
        // notícia é o que faz o Responsável ligar para a clínica três vezes.
        var animal = await _animalRepo.ObterPorIdAsync(internacao.AnimalId);

        if (animal is not null)
        {
            await _notificacoes.CriarAsync(new CriarNotificacaoDto
            {
                TutorId = animal.TutorId,
                Tipo = TipoNotificacao.AtualizacaoInternacao,
                Titulo = $"Atualizacao da internacao de {animal.Nome}",
                Corpo = $"{dto.Procedimentos.Count} procedimento(s) registrado(s) hoje. " +
                        "Toque para ver a evolucao.",
                AnimalId = animal.Id,
                Destino = $"/internacoes/{internacao.Id}"
            });
        }

        return MapearParaDto(internacao);
    }

    /// <inheritdoc/>
    public async Task<AltaInternacaoDto> DarAltaAsync(Guid id)
    {
        var internacao = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Internacao", id);

        GarantirEscritaDoResponsavel(internacao);

        internacao.DarAlta();

        _repo.Atualizar(internacao);
        await _repo.SalvarAsync();

        var saldo = internacao.ValorTotalApurado - internacao.ValorCaucao;
        var animal = await _animalRepo.ObterPorIdAsync(internacao.AnimalId);

        Guid? pagamentoDoSaldo = null;

        // RN-101/RN-102: saldo positivo vira cobrança; saldo negativo é caução a
        // devolver, e devolução não se cobra — fica registrada para o acerto.
        if (saldo > 0 && animal is not null)
        {
            var cobranca = await _pagamentos.CriarCobrancaAsync(new CriarPagamentoDto
            {
                TutorId = animal.TutorId,
                InternacaoId = internacao.Id,
                Valor = saldo,
                MeioPagamento = MeioPagamento.Pix
            });

            pagamentoDoSaldo = cobranca.Id;
        }

        if (animal is not null)
        {
            await _notificacoes.CriarAsync(new CriarNotificacaoDto
            {
                TutorId = animal.TutorId,
                Tipo = TipoNotificacao.AtualizacaoInternacao,
                Titulo = $"Alta de {animal.Nome}",
                Corpo = saldo > 0
                    ? $"Internacao encerrada. Saldo a pagar: R$ {saldo:N2}."
                    : "Internacao encerrada. Nao ha saldo a pagar.",
                AnimalId = animal.Id,
                Destino = $"/internacoes/{internacao.Id}"
            });
        }

        return new AltaInternacaoDto
        {
            InternacaoId = internacao.Id,
            AnimalId = internacao.AnimalId,
            ValorCaucao = internacao.ValorCaucao,
            ValorTotalApurado = internacao.ValorTotalApurado,
            SaldoRestante = saldo,
            PagamentoDoSaldoId = pagamentoDoSaldo,
            DataAlta = internacao.DataAlta!.Value
        };
    }

    /// <summary>
    /// Quem lê uma internação: o Admin, o veterinário que acompanha e o Responsável
    /// pelo animal (RN-105/RN-106).
    /// </summary>
    private async Task GarantirLeituraAsync(Internacao internacao)
    {
        if (_usuario.EhAdmin)
            return;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId == internacao.VeterinarioId)
            return;

        if (_usuario.EhTutor && _usuario.TutorId is { } tutorId)
        {
            var animal = await _animalRepo.ObterPorIdAsync(internacao.AnimalId);

            if (animal?.TutorId == tutorId)
                return;
        }

        throw new AcessoNegadoException("RN-105", "Esta internacao nao pertence ao seu escopo de acesso.");
    }

    /// <summary>
    /// Quem escreve é quem acompanha o caso, ou o Admin (RN-105). O Responsável não
    /// registra procedimento nem dá alta ao próprio animal.
    /// </summary>
    private void GarantirEscritaDoResponsavel(Internacao internacao)
    {
        if (_usuario.EhAdmin || (_usuario.EhVeterinario && _usuario.VeterinarioId == internacao.VeterinarioId))
            return;

        throw new AcessoNegadoException("RN-105", "Esta internacao nao pertence ao seu escopo de acesso.");
    }

    /// <summary>
    /// O responsável pela internação vem do token quando quem chama é veterinário:
    /// abrir internação em nome de outro profissional deixaria o caso sob a
    /// responsabilidade de quem não o acompanha (RN-105).
    /// </summary>
    private Guid ResolverResponsavel(Guid veterinarioIdDoPedido)
    {
        if (_usuario.EhAdmin)
            return veterinarioIdDoPedido;

        if (_usuario.EhVeterinario && _usuario.VeterinarioId is { } vetId)
            return vetId;

        throw new AcessoNegadoException("RN-105",
            "Somente o veterinario ou a administracao abre internacao.");
    }

    private static InternacaoDto MapearParaDto(Internacao i) => new()
    {
        Id = i.Id,
        AnimalId = i.AnimalId,
        VeterinarioId = i.VeterinarioId,
        ValorCaucao = i.ValorCaucao,
        ValorTotalApurado = i.ValorTotalApurado,
        DataAbertura = i.DataAbertura,
        DataAlta = i.DataAlta,
        EstaAtiva = i.EstaAtiva(),
        DiasInternado = i.DiasInternado(),
        ProcedimentosDiarios = i.ProcedimentosDiarios
    };
}
