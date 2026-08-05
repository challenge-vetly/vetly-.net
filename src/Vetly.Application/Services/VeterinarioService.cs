using System.Text.RegularExpressions;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de veterinarios. Orquestra validacoes de CRMV (RN-011),
/// soft delete com retorno de agendamentos (RN-008) e gerenciamento de perfil.
/// </summary>
public class VeterinarioService : IVeterinarioService
{
    private static readonly Regex CrmvRegex = new(@"^\d{4,6}-[A-Z]{2}$", RegexOptions.Compiled);

    private readonly IVeterinarioRepository _repo;
    private readonly TimeProvider _timeProvider;

    public VeterinarioService(IVeterinarioRepository repo, TimeProvider timeProvider)
    {
        _repo = repo;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<VeterinarioDto>> ObterTodosAsync()
    {
        var vets = await _repo.ObterAtivosAsync();
        return vets.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task<VeterinarioDto> ObterPorIdAsync(Guid id)
    {
        var vet = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Veterinario", id);
        return MapearParaDto(vet);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<VeterinarioDto>> ObterPorRegiaoAsync(string uf)
    {
        var vets = await _repo.ObterPorUfAsync(uf);
        return vets.Select(MapearParaDto);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConsultaDto>> ObterAgendaAsync(Guid veterinarioId)
    {
        var consultas = await _repo.ObterAgendaFuturaAsync(veterinarioId);
        return consultas.Select(MapearConsultaParaDto);
    }

    /// <inheritdoc/>
    public async Task<VeterinarioDto> CriarAsync(CriarVeterinarioDto dto)
    {
        // RN-011: valida formato do CRMV antes de aceitar o cadastro
        ValidarCrmv(dto.Crmv);

        var existente = await _repo.ObterPorCrmvAsync(dto.Crmv);
        if (existente is not null)
            throw new BusinessRuleException("RN-011", $"CRMV '{dto.Crmv}' ja esta cadastrado na plataforma.");

        var crmv = new Crmv(dto.Crmv);
        var vet = new Veterinario(dto.Nome, crmv, dto.UfAtuacao, dto.Persona, dto.Plano);

        foreach (var esp in dto.Especialidades) vet.AdicionarEspecialidade(esp);
        foreach (var esp in dto.EspeciesAtendidas) vet.AdicionarEspecie(esp);
        if (dto.TitulacaoAcademica is not null)
            vet.AtualizarDados(dto.Nome, dto.UfAtuacao, dto.TitulacaoAcademica);

        await _repo.AdicionarAsync(vet);
        await _repo.SalvarAsync();
        return MapearParaDto(vet);
    }

    /// <inheritdoc/>
    public async Task AtualizarAsync(Guid id, CriarVeterinarioDto dto)
    {
        var vet = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Veterinario", id);

        vet.AtualizarDados(dto.Nome, dto.UfAtuacao, dto.TitulacaoAcademica);
        vet.AtualizarPlano(dto.Plano);
        _repo.Atualizar(vet);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConsultaDto>> DesativarAsync(Guid id)
    {
        var vet = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Veterinario", id);

        // RN-008: retorna agendamentos futuros antes de desativar para o controller informar o cliente
        var agendamentos = await _repo.ObterAgendaFuturaAsync(id);
        vet.Desativar();
        _repo.Atualizar(vet);
        await _repo.SalvarAsync();
        return agendamentos.Select(MapearConsultaParaDto);
    }

    /// <summary>
    /// RN-011: valida o formato do CRMV com regex.
    /// Em producao, esta etapa seria seguida de consulta a API do CFMV.
    /// </summary>
    private static void ValidarCrmv(string crmv)
    {
        if (!CrmvRegex.IsMatch(crmv))
            throw new ValidationException("crmv", $"CRMV '{crmv}' esta em formato invalido. Use o padrao XXXXXX-UF.");
    }

    private VeterinarioDto MapearParaDto(Veterinario v) => new()
    {
        Id = v.Id, Nome = v.Nome, Crmv = v.Crmv.Valor, UfAtuacao = v.UfAtuacao,
        Especialidades = v.Especialidades, EspeciesAtendidas = v.EspeciesAtendidas,
        TitulacaoAcademica = v.TitulacaoAcademica, Persona = v.Persona,
        Plano = v.Plano, Ativo = v.Ativo, EmpresaId = v.EmpresaId,
        StrikesAtivos = v.StrikesNaJanela(_timeProvider.GetUtcNow().UtcDateTime),
        SuspensoAte = v.SuspensoAte
    };

    private static ConsultaDto MapearConsultaParaDto(Consulta c) => new()
    {
        Id = c.Id, DataHora = c.DataHora, Modalidade = c.Modalidade, TipoServico = c.TipoServico,
        VeterinarioId = c.VeterinarioId, AnimalId = c.AnimalId, ResponsavelId = c.ResponsavelId,
        PreSintomas = c.PreSintomas, Status = c.Status, LockCheckoutExpiraEm = c.LockCheckoutExpiraEm,
        ContadorRemarcacoes = c.ContadorRemarcacoes, DataRealizada = c.DataRealizada,
        DiagnosticoValidado = c.DiagnosticoValidado, ProtocoloValidado = c.ProtocoloValidado
    };
}
