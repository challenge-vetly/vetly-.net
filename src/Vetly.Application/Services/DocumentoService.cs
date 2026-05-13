using Vetly.Application.DTOs.Documento;
using Vetly.Application.Exceptions;
using Vetly.Application.Factories;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Repositories;

namespace Vetly.Application.Services;

/// <summary>
/// Serviço de documentos clínicos.
/// Usa Factory Pattern para selecionar a factory correta pelo tipo de documento (RN-024).
/// </summary>
public class DocumentoService : IDocumentoService
{
    private readonly IDocumentoRepository _repo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IEnumerable<IDocumentoFactory> _factories;

    public DocumentoService(
        IDocumentoRepository repo,
        IConsultaRepository consultaRepo,
        IVeterinarioRepository vetRepo,
        IAnimalRepository animalRepo,
        IEnumerable<IDocumentoFactory> factories)
    {
        _repo = repo;
        _consultaRepo = consultaRepo;
        _vetRepo = vetRepo;
        _animalRepo = animalRepo;
        _factories = factories;
    }

    public async Task<IEnumerable<DocumentoDto>> ObterPorConsultaAsync(Guid consultaId)
    {
        var docs = await _repo.ObterPorConsultaAsync(consultaId);
        return docs.Select(MapearParaDto);
    }

    public async Task<DocumentoDto> ObterPorIdAsync(Guid id)
    {
        var doc = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Documento", id);
        return MapearParaDto(doc);
    }

    /// <summary>
    /// Gera um documento selecionando a factory pelo tipo (RN-024).
    /// Requer que o diagnóstico esteja validado antes de gerar documentos.
    /// </summary>
    public async Task<DocumentoDto> GerarAsync(Guid consultaId, TipoDocumento tipo)
    {
        var consulta = await _consultaRepo.ObterPorIdAsync(consultaId)
            ?? throw new NotFoundException("Consulta", consultaId);

        if (!consulta.PodeGerarDocumentos())
            throw new BusinessRuleException("RN-024",
                "O diagnóstico deve ser validado pelo veterinário antes de gerar documentos.");

        var vet = await _vetRepo.ObterPorIdAsync(consulta.VeterinarioId)
            ?? throw new NotFoundException("Veterinário", consulta.VeterinarioId);

        var animal = await _animalRepo.ObterPorIdAsync(consulta.AnimalId)
            ?? throw new NotFoundException("Animal", consulta.AnimalId);

        // Seleciona a factory registrada para o tipo solicitado
        var factory = _factories.FirstOrDefault(f => f.TipoSuportado == tipo)
            ?? throw new InvalidOperationException($"Nenhuma factory registrada para o tipo '{tipo}'.");

        var consultaDto = new DTOs.Consulta.ConsultaDto { Id = consulta.Id, VeterinarioId = consulta.VeterinarioId, AnimalId = consulta.AnimalId, TutorId = consulta.TutorId, DataHora = consulta.DataHora, Modalidade = consulta.Modalidade, StatusPagamento = consulta.StatusPagamento };
        var vetDto = new DTOs.Veterinario.VeterinarioDto { Id = vet.Id, Crmv = vet.Crmv.Valor, Nome = vet.Nome };
        var animalDto = new DTOs.Animal.AnimalDto { Id = animal.Id, Nome = animal.Nome, Especie = animal.Especie };

        var documento = factory.Criar(consultaDto, vetDto, animalDto);
        await _repo.AdicionarAsync(documento);
        await _repo.SalvarAsync();
        return MapearParaDto(documento);
    }

    /// <summary>Assina digitalmente o documento (RN-031).</summary>
    public async Task AssinarAsync(Guid id)
    {
        var doc = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Documento", id);
        doc.Assinar();
        _repo.Atualizar(doc);
        await _repo.SalvarAsync();
    }

    /// <summary>Cria uma versão corrigida — valida necessidade de justificativa (RN-032/033/034).</summary>
    public async Task<DocumentoDto> CorrigirAsync(Guid id, string novosDados, string? justificativa, string crmvSolicitante)
    {
        var doc = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Documento", id);

        doc.IncrementarVersao();
        _repo.Atualizar(doc);
        await _repo.SalvarAsync();
        return MapearParaDto(doc);
    }

    private static DocumentoDto MapearParaDto(Domain.Entities.Documento d) => new()
    {
        Id = d.Id, ConsultaId = d.ConsultaId, InternacaoId = d.InternacaoId,
        TipoDocumento = d.TipoDocumento, Versao = d.Versao,
        DataGeracao = d.DataGeracao, CrmvSignatario = d.CrmvSignatario,
        AssinadoDigitalmente = d.AssinadoDigitalmente
    };
}
