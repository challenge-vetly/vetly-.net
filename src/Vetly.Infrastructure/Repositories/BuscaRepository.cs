using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Leitura do matching (§6.3).
///
/// Sem PostGIS e sem Oracle Spatial: o banco faz o que sabe fazer rápido — filtro de
/// elegibilidade e comparação de intervalo nas colunas de coordenada, que usam índice.
/// Trigonometria e score ficam em memória, sobre dezenas de linhas.
/// </summary>
public class BuscaRepository : IBuscaRepository
{
    private readonly VetlyDbContext _context;

    public BuscaRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<CandidatosDoMatching> ObterCandidatosAsync(
        decimal latMin, decimal latMax, decimal lngMin, decimal lngMax)
    {
        // Elegibilidade do perfil: publicado, CRMV valido junto ao conselho e ativo no
        // matching (RN-107). Perfil pendente de validacao nao aparece.
        var elegiveis = _context.Veterinarios
            .Where(v => v.Ativo
                        && v.Publicado
                        && v.CrmvStatus == StatusCrmv.Valido
                        && v.MatchingStatus == StatusMatching.Ativo);

        // Autonomo aparece sozinho na busca; vinculado aparece atraves da clinica
        // (produto §3.2, RN-003).
        var autonomos = await elegiveis
            .Where(v => v.Persona == PersonaVeterinario.Autonomo
                        && v.Endereco != null
                        && v.Endereco.Latitude >= latMin && v.Endereco.Latitude <= latMax
                        && v.Endereco.Longitude >= lngMin && v.Endereco.Longitude <= lngMax)
            .ToListAsync();

        var empresas = await _context.Empresas
            .Where(e => e.Ativa
                        && e.Endereco != null
                        && e.Endereco.Latitude >= latMin && e.Endereco.Latitude <= latMax
                        && e.Endereco.Longitude >= lngMin && e.Endereco.Longitude <= lngMax)
            .ToListAsync();

        var idsDasEmpresas = empresas.Select(e => e.Id).ToList();

        // Os vinculados nao precisam ter coordenada propria: quem tem endereco e a unidade
        var vinculados = await elegiveis
            .Where(v => v.EmpresaId != null && idsDasEmpresas.Contains(v.EmpresaId.Value))
            .ToListAsync();

        var vinculadosPorEmpresa = vinculados
            .GroupBy(v => v.EmpresaId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Clinica sem nenhum profissional publicado nao tem quem atender: sai do resultado
        var empresasComEquipe = empresas.Where(e => vinculadosPorEmpresa.ContainsKey(e.Id)).ToList();

        var idsDePrestadores = autonomos.Select(v => v.Id)
            .Concat(empresasComEquipe.Select(e => e.Id))
            .ToList();

        var servicos = await _context.Servicos
            .Where(s => s.Ativo && idsDePrestadores.Contains(s.PrestadorId))
            .ToListAsync();

        var servicosPorPrestador = servicos
            .GroupBy(s => s.PrestadorId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new CandidatosDoMatching(
            autonomos, empresasComEquipe, vinculadosPorEmpresa, servicosPorPrestador);
    }

    /// <inheritdoc/>
    public async Task<(decimal Latitude, decimal Longitude)?> ObterCoordenadaDoCepAsync(string cep)
    {
        var normalizado = CepCoordenada.SomenteDigitos(cep);

        var encontrado = await _context.CepCoordenadas
            .Where(c => c.Cep == normalizado)
            .Select(c => new { c.Latitude, c.Longitude })
            .FirstOrDefaultAsync();

        return encontrado is null ? null : (encontrado.Latitude, encontrado.Longitude);
    }
}
