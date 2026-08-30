using Vetly.Application.DTOs.Agenda;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço da agenda do veterinário: configuração, materialização de horários,
/// disponibilidade e vitrine de serviços (RN-032/RN-034/RN-035).
/// </summary>
public class AgendaService : IAgendaService
{
    private readonly IAgendaRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IUsuarioAtual _usuario;

    public AgendaService(IAgendaRepository repo, IVeterinarioRepository vetRepo, IUsuarioAtual usuario)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public async Task<AgendaConfigDto> ConfigurarAsync(Guid veterinarioId, ConfigurarAgendaDto dto)
    {
        await GarantirGestaoDaAgendaAsync(veterinarioId);

        var dias = dto.Dias.Aggregate(DiasDaSemana.Nenhum, (acc, dia) => acc | dia.ParaFlag());
        var inicio = ParaMinutos(dto.HoraInicio);
        var fim = ParaMinutos(dto.HoraFim);

        var config = await _repo.ObterConfigAsync(veterinarioId);

        if (config is null)
        {
            config = new AgendaConfig(veterinarioId, dias, inicio, fim, dto.DuracaoMinutos, dto.IntervaloMinutos);
            await _repo.AdicionarConfigAsync(config);
        }
        else
        {
            config.Configurar(dias, inicio, fim, dto.DuracaoMinutos, dto.IntervaloMinutos);
            _repo.AtualizarConfig(config);
        }

        var materializados = await MaterializarAsync(config);
        await _repo.SalvarAsync();

        return MapearConfig(config, materializados);
    }

    /// <inheritdoc/>
    public async Task<AgendaConfigDto> ObterConfigAsync(Guid veterinarioId)
    {
        var config = await _repo.ObterConfigAsync(veterinarioId)
            ?? throw new NotFoundException("Configuracao de agenda", veterinarioId);

        return MapearConfig(config, materializados: 0);
    }

    /// <inheritdoc/>
    public async Task<DisponibilidadeDto> ObterDisponibilidadeAsync(
        Guid veterinarioId, DateTime? de = null, DateTime? ate = null)
    {
        var agora = DateTime.UtcNow;
        var inicio = de ?? agora;
        var fim = ate ?? inicio.AddDays(14);

        if (fim <= inicio)
            throw new ValidationException("periodo", "A data final deve ser depois da data inicial.");

        var slots = await _repo.ObterSlotsAsync(veterinarioId, inicio, fim);

        // Horario disponivel e o livre e tambem o que esta em checkout com lock vencido:
        // o lock caduca na leitura, sem depender do job de expiracao (RN-035).
        var livres = slots.Where(s => s.EstaDisponivel(agora) && s.Inicio > agora).ToList();

        return new DisponibilidadeDto
        {
            VeterinarioId = veterinarioId,
            TotalDeHorariosLivres = livres.Count,
            Dias = [.. livres
                .GroupBy(s => DateOnly.FromDateTime(s.Inicio))
                .OrderBy(g => g.Key)
                .Select(g => new DiaDisponivelDto
                {
                    Data = g.Key,
                    Horarios = [.. g.OrderBy(s => s.Inicio).Select(MapearSlot)]
                })]
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ServicoDto>> ObterServicosAsync(Guid prestadorId)
    {
        var servicos = await _repo.ObterServicosAsync(prestadorId);
        return servicos.Select(MapearServico);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ServicoDto>> DefinirServicosAsync(Guid prestadorId, DefinirServicosDto dto)
    {
        await GarantirGestaoDaAgendaAsync(prestadorId);

        var existentes = (await _repo.ObterServicosAsync(prestadorId)).ToList();
        var tiposInformados = dto.Servicos.Select(s => s.Tipo).ToHashSet();

        if (tiposInformados.Count != dto.Servicos.Count)
            throw new ValidationException("servicos", "Ha tipos de servico repetidos na lista.");

        foreach (var informado in dto.Servicos)
        {
            var existente = existentes.FirstOrDefault(s => s.Tipo == informado.Tipo);

            if (existente is null)
            {
                await _repo.AdicionarServicoAsync(new Servico(
                    prestadorId, informado.Tipo, informado.Valor, informado.DuracaoMinutos, informado.AceitaPlanoPet));
            }
            else
            {
                existente.Atualizar(informado.Valor, informado.DuracaoMinutos, informado.AceitaPlanoPet);
                _repo.AtualizarServico(existente);
            }
        }

        // Servico que saiu da lista e desativado, nao apagado: consulta antiga aponta
        // para ele, e o historico nao pode ficar orfao.
        foreach (var removido in existentes.Where(s => !tiposInformados.Contains(s.Tipo)))
        {
            removido.Desativar();
            _repo.AtualizarServico(removido);
        }

        await _repo.SalvarAsync();
        return (await _repo.ObterServicosAsync(prestadorId)).Select(MapearServico);
    }

    /// <summary>
    /// Materializa os horários do horizonte configurado, pulando os instantes que já
    /// existem. Rematerializar depois de mudar a agenda não duplica horário nem apaga
    /// o que já foi agendado.
    /// </summary>
    private async Task<int> MaterializarAsync(AgendaConfig config)
    {
        var hoje = DateTime.UtcNow.Date;
        var jaExistem = await _repo.ObterIniciosMaterializadosAsync(config.VeterinarioId, hoje);

        var novos = new List<Slot>();

        for (var dia = 0; dia < AgendaConfig.DiasDeHorizonte; dia++)
        {
            var data = hoje.AddDays(dia);

            foreach (var (inicio, fim) in config.GerarHorariosDoDia(data))
            {
                if (inicio <= DateTime.UtcNow || jaExistem.Contains(inicio))
                    continue;

                novos.Add(new Slot(config.VeterinarioId, inicio, fim));
            }
        }

        if (novos.Count > 0)
            await _repo.AdicionarSlotsAsync(novos);

        return novos.Count;
    }

    /// <summary>
    /// O veterinário gerencia a própria agenda; o Admin gerencia a dos vinculados
    /// (RN-105/RN-106).
    /// </summary>
    private async Task GarantirGestaoDaAgendaAsync(Guid veterinarioId)
    {
        _ = await _vetRepo.ObterPorIdAsync(veterinarioId)
            ?? throw new NotFoundException("Veterinario", veterinarioId);

        if (_usuario.EhAdmin || _usuario.VeterinarioId == veterinarioId)
            return;

        throw new AcessoNegadoException("RN-105", "Esta agenda nao pertence ao seu escopo de acesso.");
    }

    private static int ParaMinutos(string horaHhMm)
    {
        var partes = horaHhMm.Split(':');
        return (int.Parse(partes[0]) * 60) + int.Parse(partes[1]);
    }

    private static string ParaHhMm(int minutos) => $"{minutos / 60:D2}:{minutos % 60:D2}";

    private static AgendaConfigDto MapearConfig(AgendaConfig config, int materializados) => new()
    {
        VeterinarioId = config.VeterinarioId,
        Dias = [.. Enum.GetValues<DayOfWeek>().Where(d => config.Dias.Atende(d))],
        HoraInicio = ParaHhMm(config.InicioEmMinutos),
        HoraFim = ParaHhMm(config.FimEmMinutos),
        DuracaoMinutos = config.DuracaoMinutos,
        IntervaloMinutos = config.IntervaloMinutos,
        AtualizadaEm = config.AtualizadaEm,
        SlotsMaterializados = materializados,
        MaterializadaAte = DateTime.UtcNow.Date.AddDays(AgendaConfig.DiasDeHorizonte)
    };

    private static SlotDto MapearSlot(Slot s) => new()
    {
        Id = s.Id,
        VeterinarioId = s.VeterinarioId,
        Inicio = s.Inicio,
        Fim = s.Fim,
        Estado = s.Estado
    };

    private static ServicoDto MapearServico(Servico s) => new()
    {
        Id = s.Id,
        PrestadorId = s.PrestadorId,
        Tipo = s.Tipo,
        Valor = s.Valor,
        AceitaPlanoPet = s.AceitaPlanoPet,
        DuracaoMinutos = s.DuracaoMinutos,
        Ativo = s.Ativo
    };
}
