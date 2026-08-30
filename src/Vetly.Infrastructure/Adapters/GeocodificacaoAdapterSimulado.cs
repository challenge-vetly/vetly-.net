using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Geocodificação simulada (camada C2), apoiada na tabela seed TB_CEP_COORDENADA
/// (RN-026, §5.6). Não faz chamada de rede nenhuma.
///
/// Ordem de resolução:
/// <list type="number">
///   <item><description>CEP exato na base → precisão <c>Cep</c>, coordenada confiável;</description></item>
///   <item><description>CEP desconhecido, mas cidade/UF conhecidas → centro aproximado da
///   cidade (média das coordenadas conhecidas), precisão <c>Bairro</c> e marcada para
///   revisão — serve para não travar o cadastro, mas não é boa o bastante para o
///   matching confiar nela;</description></item>
///   <item><description>Nada reconhecido → <c>Desconhecida</c>, sem coordenada. Não se
///   inventa posição: um prestador no lugar errado do mapa é pior que um prestador sem
///   posição, que simplesmente não aparece na busca por proximidade.</description></item>
/// </list>
///
/// A precisão <c>Endereco</c> não é produzida aqui: exigiria número e logradouro
/// resolvidos, que é o que o fornecedor real entrega (pendência P-02).
/// </summary>
public class GeocodificacaoAdapterSimulado : IGeocodificacaoAdapter
{
    private readonly VetlyDbContext _context;
    private readonly ILogger<GeocodificacaoAdapterSimulado> _logger;

    public GeocodificacaoAdapterSimulado(
        VetlyDbContext context, ILogger<GeocodificacaoAdapterSimulado> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CoordenadaDto> GeocodificarAsync(EnderecoDto endereco)
    {
        ArgumentNullException.ThrowIfNull(endereco);

        var cep = CepCoordenada.SomenteDigitos(endereco.Cep);

        var porCep = await _context.CepCoordenadas.FirstOrDefaultAsync(c => c.Cep == cep);

        if (porCep is not null)
        {
            return new CoordenadaDto
            {
                Latitude = porCep.Latitude,
                Longitude = porCep.Longitude,
                Precisao = PrecisaoCoordenada.Cep,
                Revisar = false
            };
        }

        var cidade = endereco.Cidade?.Trim() ?? string.Empty;
        var uf = endereco.Uf?.Trim().ToUpperInvariant() ?? string.Empty;

        var daCidade = await _context.CepCoordenadas
            .Where(c => c.Cidade == cidade && c.Uf == uf)
            .Select(c => new { c.Latitude, c.Longitude })
            .ToListAsync();

        if (daCidade.Count > 0)
        {
            _logger.LogInformation(
                "CEP {Cep} desconhecido; coordenada aproximada pelo centro de {Cidade}/{Uf} e marcada para revisao.",
                cep, cidade, uf);

            return new CoordenadaDto
            {
                Latitude = Math.Round(daCidade.Average(c => c.Latitude), 6),
                Longitude = Math.Round(daCidade.Average(c => c.Longitude), 6),
                Precisao = PrecisaoCoordenada.Bairro,
                Revisar = true
            };
        }

        _logger.LogWarning(
            "Endereco nao geocodificado: CEP {Cep}, cidade {Cidade}/{Uf}. O prestador fica sem posicao.",
            cep, cidade, uf);

        return new CoordenadaDto { Precisao = PrecisaoCoordenada.Desconhecida, Revisar = true };
    }
}
