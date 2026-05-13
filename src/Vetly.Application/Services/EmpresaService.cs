using Vetly.Application.DTOs.Empresa;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Repositories;

namespace Vetly.Application.Services;

/// <summary>Serviço de empresas. Gerencia cadastro e vinculação de veterinários.</summary>
public class EmpresaService : IEmpresaService
{
    private readonly IEmpresaRepository _repo;
    private readonly IVeterinarioRepository _vetRepo;

    public EmpresaService(IEmpresaRepository repo, IVeterinarioRepository vetRepo)
    {
        _repo = repo;
        _vetRepo = vetRepo;
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
        _ = await _repo.ObterPorIdAsync(empresaId)
            ?? throw new NotFoundException("Empresa", empresaId);

        var vet = await _vetRepo.ObterPorIdAsync(veterinarioId)
            ?? throw new NotFoundException("Veterinário", veterinarioId);

        vet.VincularEmpresa(empresaId);
        _vetRepo.Atualizar(vet);
        await _vetRepo.SalvarAsync();
    }

    private static EmpresaDto MapearParaDto(Empresa e) => new()
    {
        Id = e.Id, Nome = e.Nome, Tipo = e.Tipo,
        AdministradorId = e.AdministradorId, Ativa = e.Ativa
    };
}
