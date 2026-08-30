using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Empresa;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>Servico de empresas. Gerencia cadastro e vinculacao de veterinarios.</summary>
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
        var empresa = new Empresa(dto.Nome, dto.Tipo, dto.AdministradorId,
            dto.Plano ?? Vetly.Domain.Enums.PlanoAssinatura.Basico);
        AplicarConfiguracaoDaUnidade(empresa, dto);

        await _repo.AdicionarAsync(empresa);
        await _repo.SalvarAsync();
        return MapearParaDto(empresa);
    }

    public async Task AtualizarAsync(Guid id, CriarEmpresaDto dto)
    {
        var empresa = await _repo.ObterPorIdAsync(id)
            ?? throw new NotFoundException("Empresa", id);
        empresa.AtualizarDados(dto.Nome, dto.Tipo);
        if (dto.Plano is not null)
            empresa.AtualizarPlano(dto.Plano.Value);
        AplicarConfiguracaoDaUnidade(empresa, dto);

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

        // RN-072: a faixa Enterprise troca automaticamente ao cruzar o limite de vets
        await RecalcularFaixaAsync(empresa);
    }

    /// <summary>
    /// Recalcula e persiste a faixa Enterprise da unidade a partir do numero de
    /// veterinarios vinculados (RN-072). Fora do Enterprise nao ha faixa.
    /// </summary>
    private async Task RecalcularFaixaAsync(Empresa empresa)
    {
        var vinculados = await _vetRepo.ObterPorEmpresaAsync(empresa.Id);
        empresa.RecalcularFaixaEnterprise(vinculados.Count());

        _repo.Atualizar(empresa);
        await _repo.SalvarAsync();
    }

    /// <summary>
    /// Aplica endereco (RN-026) e politica de retencao (RN-042) vindos do DTO.
    /// Percentual omitido preserva o valor atual — o default de 30% ja vem do construtor.
    /// </summary>
    private static void AplicarConfiguracaoDaUnidade(Empresa empresa, CriarEmpresaDto dto)
    {
        if (dto.Endereco is not null)
        {
            empresa.DefinirEndereco(new Endereco(
                dto.Endereco.Cep, dto.Endereco.Logradouro, dto.Endereco.Numero,
                dto.Endereco.Bairro, dto.Endereco.Cidade, dto.Endereco.Uf, dto.Endereco.Complemento));
        }

        if (dto.PercentualRetencaoParcial is not null)
            empresa.DefinirPoliticaRetencao(dto.PercentualRetencaoParcial.Value);
    }

    private static EmpresaDto MapearParaDto(Empresa e) => new()
    {
        Id = e.Id, Nome = e.Nome, Tipo = e.Tipo,
        AdministradorId = e.AdministradorId, Ativa = e.Ativa,
        PercentualRetencaoParcial = e.PercentualRetencaoParcial,
        Plano = e.Plano, FaixaEnterprise = e.FaixaEnterprise,
        Endereco = e.Endereco is null ? null : new EnderecoDto
        {
            Cep = e.Endereco.Cep, Logradouro = e.Endereco.Logradouro, Numero = e.Endereco.Numero,
            Complemento = e.Endereco.Complemento, Bairro = e.Endereco.Bairro,
            Cidade = e.Endereco.Cidade, Uf = e.Endereco.Uf,
            Latitude = e.Endereco.Latitude, Longitude = e.Endereco.Longitude,
            CoordenadaRevisar = e.Endereco.CoordenadaRevisar
        }
    };
}
