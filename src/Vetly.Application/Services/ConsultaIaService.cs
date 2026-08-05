using Microsoft.Extensions.Configuration;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Orquestra a IA dentro do ciclo de vida da consulta (RN-096..100). O <see cref="IOllamaService"/>
/// só fala com o modelo; este serviço decide o que perguntar (contexto acessível ao vet
/// naquele atendimento — a colmeia não amplia o acesso da IA, RN-096), grava a trilha de
/// auditoria e aplica a decisão do veterinário ao estado final da consulta (RN-099).
/// </summary>
public class ConsultaIaService : IConsultaIaService
{
    private readonly IConsultaRepository _consultaRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly ILogAuditoriaIARepository _logRepo;
    private readonly IOllamaService _ollama;
    private readonly TimeProvider _timeProvider;
    private readonly string _versaoModelo;

    public ConsultaIaService(
        IConsultaRepository consultaRepo,
        IAnimalRepository animalRepo,
        IVeterinarioRepository vetRepo,
        ILogAuditoriaIARepository logRepo,
        IOllamaService ollama,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _consultaRepo = consultaRepo;
        _animalRepo = animalRepo;
        _vetRepo = vetRepo;
        _logRepo = logRepo;
        _ollama = ollama;
        _timeProvider = timeProvider;
        _versaoModelo = configuration["Ollama:Model"] ?? "llama3.1";
    }

    /// <inheritdoc/>
    public async Task<SugestaoDiagnosticoResponseDto> SugerirDiagnosticoAsync(Guid consultaId)
    {
        var (consulta, animal, vet) = await CarregarContextoAsync(consultaId);

        var contexto = new DTOs.IA.ContextoClinicoDto
        {
            Especie = animal.Especie,
            Raca = animal.Raca,
            IdadeAnos = animal.IdadeEmAnos(),
            PesoKg = animal.PesoKg ?? 0,
            Sintomas = string.IsNullOrWhiteSpace(consulta.PreSintomas) ? [] : [consulta.PreSintomas],
            HistoricoRelevante = string.Join("; ", animal.CondicoesPreExistentes)
        };

        var hipoteses = await _ollama.SugerirDiagnosticoAsync(contexto);
        var conteudoSugerido = System.Text.Json.JsonSerializer.Serialize(hipoteses);

        var log = await RegistrarSugestaoAsync(consulta, vet, TipoSugestaoIA.Diagnostico, conteudoSugerido);

        return new SugestaoDiagnosticoResponseDto { Hipoteses = hipoteses, LogId = log.Id };
    }

    /// <inheritdoc/>
    public async Task<SugestaoProtocoloResponseDto> SugerirProtocoloAsync(Guid consultaId)
    {
        var (consulta, animal, vet) = await CarregarContextoAsync(consultaId);

        // RN-096.2: recusa antes de qualquer chamada ao modelo se o peso nao estiver cadastrado.
        if (animal.PesoKg is not { } pesoKg)
            throw new BusinessRuleException("IA-001", "O peso do animal e obrigatorio para calcular a dose.");

        var diagnosticoBase = consulta.DiagnosticoFinal ?? consulta.PreSintomas ?? string.Empty;
        var protocolo = await _ollama.SugerirProtocoloAsync(diagnosticoBase, animal.Especie, pesoKg);

        // Cruza os medicamentos sugeridos com as medicações em uso do animal (RN-096.2).
        var alertasInteracao = animal.MedicacoesEmUso.Count == 0
            ? []
            : protocolo.Medicamentos
                .Where(m => animal.MedicacoesEmUso.Any(uso => m.Contains(uso, StringComparison.OrdinalIgnoreCase)))
                .Select(m => $"Possivel interacao entre '{m}' e medicacao em uso.")
                .ToList();

        var conteudoSugerido = System.Text.Json.JsonSerializer.Serialize(protocolo);
        var log = await RegistrarSugestaoAsync(consulta, vet, TipoSugestaoIA.Protocolo, conteudoSugerido);

        return new SugestaoProtocoloResponseDto
        {
            Medicamentos = protocolo.Medicamentos, AlertasInteracao = alertasInteracao,
            DuracaoEstimada = protocolo.DuracaoEstimada, Observacoes = protocolo.Observacoes, LogId = log.Id
        };
    }

    /// <inheritdoc/>
    public async Task<RegistrarDecisaoIAResponseDto> RegistrarDecisaoAsync(Guid consultaId, RegistrarDecisaoIADto dto)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (dto.Decisao == DecisaoVeterinario.Corrigir && string.IsNullOrWhiteSpace(dto.ConteudoCorrigido))
            throw new BusinessRuleException("IA-003", "O conteudo corrigido e obrigatorio ao escolher 'Corrigir'.");

        var log = await _logRepo.ObterPendenteAsync(consultaId, dto.Tipo)
            ?? throw new NotFoundException(
                $"Nao ha sugestao de IA pendente de decisao para o tipo '{dto.Tipo}' nesta consulta.");

        // RN-099: Aprovar -> o sugerido vira final; Corrigir -> o texto do vet vira final
        // (a IA nao re-infere nada clinico); Nao aprovar -> encerra sem conteudo final.
        string? conteudoFinal = dto.Decisao switch
        {
            DecisaoVeterinario.Aprovar => log.ConteudoSugerido,
            DecisaoVeterinario.Corrigir => dto.ConteudoCorrigido,
            _ => null
        };

        log.RegistrarDecisao(dto.Decisao, conteudoFinal);
        _logRepo.Atualizar(log);
        await _logRepo.SalvarAsync();

        if (conteudoFinal is not null)
        {
            if (dto.Tipo == TipoSugestaoIA.Diagnostico)
                consulta.DefinirDiagnosticoFinal(conteudoFinal);
            else
                consulta.DefinirProtocoloFinal(conteudoFinal);

            _consultaRepo.Atualizar(consulta);
            await _consultaRepo.SalvarAsync();
        }

        return new RegistrarDecisaoIAResponseDto { EstadoFinalDefinido = consulta.EstadoFinalDefinido };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<LogAuditoriaIADto>> ObterAuditoriaAsync(Guid consultaId)
    {
        _ = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        var logs = await _logRepo.ObterPorConsultaAsync(consultaId);
        return logs.Select(MapearParaDto);
    }

    private async Task<(Consulta consulta, Animal animal, Veterinario vet)> CarregarContextoAsync(Guid consultaId)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);
        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId)
            ?? throw new NotFoundException("Animal", consulta.AnimalId);
        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("Veterinario", consulta.VeterinarioId);

        return (consulta, animal, vet);
    }

    private async Task<LogAuditoriaIA> RegistrarSugestaoAsync(
        Consulta consulta, Veterinario vet, TipoSugestaoIA tipo, string conteudoSugerido)
    {
        var log = new LogAuditoriaIA(
            consulta.Id, vet.Id, vet.Crmv.Valor, _versaoModelo, tipo, conteudoSugerido,
            _timeProvider.GetUtcNow().UtcDateTime);

        await _logRepo.AdicionarAsync(log);
        await _logRepo.SalvarAsync();
        return log;
    }

    private static LogAuditoriaIADto MapearParaDto(LogAuditoriaIA l) => new()
    {
        Id = l.Id, ConsultaId = l.ConsultaId, VeterinarioId = l.VeterinarioId, Crmv = l.Crmv,
        Timestamp = l.Timestamp, VersaoModelo = l.VersaoModelo, TipoSugestao = l.TipoSugestao,
        ConteudoSugerido = l.ConteudoSugerido, Decisao = l.Decisao, ConteudoFinal = l.ConteudoFinal,
        Pendente = l.Pendente
    };
}
