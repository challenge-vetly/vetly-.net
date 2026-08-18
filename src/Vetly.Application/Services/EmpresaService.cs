using Vetly.Application.DTOs.Empresa;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Services;

/// <summary>
/// Servico de empresas. Gerencia cadastro, vinculacao de veterinarios e o dashboard
/// financeiro consolidado do Administrador (RN-007), com vedacoes por construcao do DTO
/// e autorizacao por posse via ICurrentUserService (RN-001..006).
/// </summary>
public class EmpresaService : IEmpresaService
{
    private readonly IEmpresaRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;
    private readonly IConsultaRepository _consultaRepo;
    private readonly IPagamentoRepository _pagamentoRepo;
    private readonly ICurrentUserService _currentUser;

    public EmpresaService(
        IEmpresaRepository repo, IVeterinarioRepository vetRepo, IConsultaRepository consultaRepo,
        IPagamentoRepository pagamentoRepo, ICurrentUserService currentUser)
    {
        _repo = repo;
        _vetRepo = vetRepo;
        _consultaRepo = consultaRepo;
        _pagamentoRepo = pagamentoRepo;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<EmpresaDto>> ObterTodosAsync()
    {
        var empresas = await _repo.ObterAtivasAsync();
        return empresas.Select(MapearParaDto);
    }

    public async Task<EmpresaDto> ObterPorIdAsync(Guid id)
    {
        var empresa = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Empresa", id);
        return MapearParaDto(empresa);
    }

    public async Task<EmpresaDto> CriarAsync(CriarEmpresaDto dto)
    {
        var empresa = new Empresa(dto.Nome, dto.Tipo, dto.AdministradorId);
        await _repo.AdicionarAsync(empresa);
        await _repo.SalvarAsync();
        return MapearParaDto(empresa);
    }

    public async Task AtualizarAsync(Guid id, CriarEmpresaDto dto)
    {
        var empresa = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Empresa", id);
        empresa.AtualizarDados(dto.Nome, dto.Tipo);
        _repo.Atualizar(empresa);
        await _repo.SalvarAsync();
    }

    public async Task DesativarAsync(Guid id)
    {
        var empresa = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Empresa", id);
        empresa.Desativar();
        _repo.Atualizar(empresa);
        await _repo.SalvarAsync();
    }

    public async Task<IEnumerable<VeterinarioDto>> ObterVeterinariosAsync(Guid empresaId)
    {
        var vets = await _vetRepo.ObterPorEmpresaAsync(empresaId);
        return vets.Select(v => new VeterinarioDto
        {
            Id = v.Id, Nome = v.Nome, Crmv = v.Crmv.Valor, UfAtuacao = v.UfAtuacao,
            Persona = v.Persona, Plano = v.Plano, Ativo = v.Ativo, EmpresaId = v.EmpresaId
        });
    }

    public async Task VincularVeterinarioAsync(Guid empresaId, Guid veterinarioId)
    {
        var empresa = await _repo.ObterPorIdAsync(empresaId)
            ?? throw new NotFoundException("Empresa", empresaId);

        var vet = await _vetRepo.ObterPorIdAsync(veterinarioId)
            ?? throw new NotFoundException("Veterinario", veterinarioId);

        vet.VincularEmpresa(empresaId);
        _vetRepo.Atualizar(vet);
        await _vetRepo.SalvarAsync();

        // RN-092: a faixa Enterprise muda de degrau ao cruzar o limite de vets.
        var qtdVets = (await _vetRepo.ObterPorEmpresaAsync(empresaId)).Count();
        empresa.RecalcularFaixaEnterprise(qtdVets);
        _repo.Atualizar(empresa);
        await _repo.SalvarAsync();
    }

    /// <inheritdoc/>
    public async Task<DashboardConsolidadoDto> ObterDashboardConsolidadoAsync(Guid empresaId)
    {
        var empresa = await _repo.ObterPorIdAsync(empresaId)
            ?? throw new NotFoundException("Empresa", empresaId);
        ChecarPosseDaEmpresa(empresaId);

        var vets = (await _vetRepo.ObterPorEmpresaAsync(empresaId)).ToList();
        var vetIds = vets.Select(v => v.Id).ToList();

        empresa.RecalcularFaixaEnterprise(vets.Count);
        _repo.Atualizar(empresa);
        await _repo.SalvarAsync();

        var consultas = (await _consultaRepo.ObterPorVeterinariosAsync(vetIds)).ToList();
        var pagamentos = (await _pagamentoRepo.ObterPorVeterinariosAsync(vetIds)).ToList();

        return new DashboardConsolidadoDto
        {
            EmpresaId = empresaId,
            QtdVeterinariosAtivos = vets.Count,
            FaixaEnterprise = empresa.FaixaEnterprise,
            FaturamentoBruto = pagamentos.Sum(p => p.Valor),
            TotalComissoes = pagamentos.Sum(p => p.ValorComissao),
            TotalRepasses = pagamentos.Sum(p => p.ValorRepasse),
            TotalReembolsos = pagamentos.Sum(p => p.ValorEstornado ?? 0m),
            QtdConsultasRealizadas = consultas.Count(c => c.Status == StatusConsulta.Realizada),
            QtdConsultasCanceladas = consultas.Count(c => c.Status == StatusConsulta.Cancelada)
        };
    }

    /// <inheritdoc/>
    public async Task<AssinaturaEmpresaDto> ObterAssinaturaAsync(Guid empresaId)
    {
        var empresa = await _repo.ObterPorIdAsync(empresaId)
            ?? throw new NotFoundException("Empresa", empresaId);
        ChecarPosseDaEmpresa(empresaId);

        var qtdVets = (await _vetRepo.ObterPorEmpresaAsync(empresaId)).Count();
        empresa.RecalcularFaixaEnterprise(qtdVets);
        _repo.Atualizar(empresa);
        await _repo.SalvarAsync();

        return new AssinaturaEmpresaDto
        {
            EmpresaId = empresaId, QtdVeterinariosAtivos = qtdVets, FaixaEnterprise = empresa.FaixaEnterprise
        };
    }

    /// <summary>
    /// RN-001..006/007: um Admin só acessa dados financeiros/assinatura da própria empresa —
    /// tentativa cruzada é 403. Sem checagem quando não há claim (dev-stub sem entidadeId).
    /// </summary>
    private void ChecarPosseDaEmpresa(Guid empresaId)
    {
        if (_currentUser.Role == "Admin" && _currentUser.EntidadeId is { } id && id != empresaId)
            throw new ForbiddenException("ACESSO-002", "Administrador so pode acessar dados da propria empresa.");
    }

    private static EmpresaDto MapearParaDto(Empresa e) => new()
    {
        Id = e.Id, Nome = e.Nome, Tipo = e.Tipo,
        AdministradorId = e.AdministradorId, Ativa = e.Ativa, FaixaEnterprise = e.FaixaEnterprise
    };
}
