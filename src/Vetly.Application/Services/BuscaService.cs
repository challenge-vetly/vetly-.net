using Vetly.Application.DTOs.Busca;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.ValueObjects;

namespace Vetly.Application.Services;

/// <summary>
/// Matching por geolocalização (RN-001 a RN-033).
///
/// O caminho é o da §6.3: o banco reduz o universo por elegibilidade e bounding box,
/// e aqui se calcula a distância real, se aplicam os filtros eliminatórios e se ordena
/// pelo score de 40/30/30.
/// </summary>
public class BuscaService : IBuscaService
{
    /// <summary>Raio padrão da busca, em quilômetros (RN-028).</summary>
    public const double RaioPadraoKm = 10;

    /// <summary>Raio máximo que o Responsável pode pedir (RN-028).</summary>
    public const double RaioMaximoKm = 25;

    /// <summary>Mínimo de avaliações para a nota valer publicamente (RN-057).</summary>
    public const int MinimoDeAvaliacoes = 3;

    /// <summary>Duração do selo "Novo na Vetly", em dias (RN-033).</summary>
    public const int DiasDoSeloNovo = 30;

    /// <summary>Teto de horários em 48h considerado no fator de disponibilidade (P-09).</summary>
    private const int TetoDeHorarios48h = 10;

    // Pesos do score (RN-030)
    private const double PesoDistancia = 0.40;
    private const double PesoAvaliacao = 0.30;
    private const double PesoDisponibilidade = 0.30;

    private readonly IBuscaRepository _repo;
    private readonly IAnimalRepository _animalRepo;
    private readonly IAgendaRepository _agendaRepo;
    private readonly IUsuarioAtual _usuario;

    public BuscaService(
        IBuscaRepository repo,
        IAnimalRepository animalRepo,
        IAgendaRepository agendaRepo,
        IUsuarioAtual usuario)
    {
        _repo = repo;
        _animalRepo = animalRepo;
        _agendaRepo = agendaRepo;
        _usuario = usuario;
    }

    /// <summary>Candidato com a coordenada ainda à mão, antes de virar resultado.</summary>
    private sealed record Candidato(PrestadorEncontradoDto Dto, decimal Latitude, decimal Longitude);

    /// <inheritdoc/>
    public async Task<ResultadoBuscaDto> BuscarAsync(FiltroBuscaDto filtro, Paginacao paginacao)
    {
        var animal = await _animalRepo.ObterPorIdAsync(filtro.AnimalId)
            ?? throw new NotFoundException("Animal", filtro.AnimalId);

        if (_usuario.EhTutor && _usuario.TutorId != animal.TutorId)
            throw new AcessoNegadoException("RN-105", "Este animal nao pertence ao seu escopo de acesso.");

        var (latitude, longitude, origem) = await ResolverPosicaoAsync(filtro);
        var raioKm = Math.Clamp(filtro.RaioKm ?? RaioPadraoKm, 1, RaioMaximoKm);

        var caixa = Geo.CalcularBoundingBox(latitude, longitude, raioKm);
        var candidatos = await _repo.ObterCandidatosAsync(caixa.LatMin, caixa.LatMax, caixa.LngMin, caixa.LngMax);

        var agora = DateTime.UtcNow;
        var idsDeVets = IdsDosVeterinarios(candidatos);
        var disponibilidade = idsDeVets.Count == 0
            ? []
            : await _agendaRepo.ContarDisponiveisNasProximas48hAsync(idsDeVets, agora);
        var proximos = idsDeVets.Count == 0
            ? []
            : await _agendaRepo.ObterProximoHorarioLivreAsync(idsDeVets, agora);

        var montados = new List<Candidato>();

        foreach (var vet in candidatos.Autonomos)
        {
            var candidato = MontarDoAutonomo(vet, disponibilidade, proximos, agora);
            if (candidato is not null) montados.Add(candidato);
        }

        foreach (var empresa in candidatos.Empresas)
        {
            var candidato = MontarDaEmpresa(empresa, candidatos, disponibilidade, proximos, agora);
            if (candidato is not null) montados.Add(candidato);
        }

        var resultados = new List<PrestadorEncontradoDto>();

        foreach (var candidato in montados)
        {
            if (Elegivel(candidato, candidatos, filtro, animal, latitude, longitude, raioKm, agora))
                resultados.Add(candidato.Dto);
        }

        // Desempate da RN-031: maior nota, menor distancia, maior disponibilidade em 48h
        var ordenados = resultados
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.Nota ?? 0)
            .ThenBy(r => r.DistanciaKm)
            .ThenByDescending(r => r.HorariosLivres48h)
            .ToList();

        return new ResultadoBuscaDto
        {
            Itens = [.. ordenados.Skip(paginacao.Deslocamento).Take(paginacao.Tamanho)],
            Total = ordenados.Count,
            Pagina = paginacao.Pagina,
            Tamanho = paginacao.Tamanho,
            RaioAplicadoKm = raioKm,
            Origem = origem,
            EspecieDoAnimal = animal.Especie
        };
    }

    /// <summary>
    /// Posição usada no cálculo de distância. GPS quando concedido; CEP informado
    /// quando a permissão de localização é negada — sem o fallback o fluxo de busca
    /// travaria (RN-027).
    /// </summary>
    private async Task<(decimal Lat, decimal Lng, OrigemDaPosicao Origem)> ResolverPosicaoAsync(FiltroBuscaDto filtro)
    {
        if (filtro.Lat is { } lat && filtro.Lng is { } lng)
        {
            if (lat is < -90 or > 90 || lng is < -180 or > 180)
                throw new ValidationException("coordenada", "Latitude ou longitude fora de faixa.");

            return (lat, lng, OrigemDaPosicao.Gps);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Cep))
        {
            var doCep = await _repo.ObterCoordenadaDoCepAsync(filtro.Cep)
                ?? throw new BusinessRuleException("RN-027",
                    "Nao foi possivel localizar o CEP informado. Tente outro CEP ou permita o acesso a localizacao.");

            return (doCep.Latitude, doCep.Longitude, OrigemDaPosicao.Cep);
        }

        throw new ValidationException("localizacao",
            "Informe a localizacao (lat/lng) ou um CEP para a busca por proximidade.");
    }

    private static List<Guid> IdsDosVeterinarios(CandidatosDoMatching candidatos) =>
        [.. candidatos.Autonomos.Select(v => v.Id)
            .Concat(candidatos.VinculadosPorEmpresa.SelectMany(kv => kv.Value).Select(v => v.Id))
            .Distinct()];

    private static Candidato? MontarDoAutonomo(
        Veterinario vet,
        Dictionary<Guid, int> disponibilidade,
        Dictionary<Guid, DateTime> proximos,
        DateTime agora)
    {
        if (vet.Endereco?.Latitude is not { } lat || vet.Endereco?.Longitude is not { } lng)
            return null;

        var temNotaPublica = vet.NumAvaliacoes >= MinimoDeAvaliacoes;

        var dto = new PrestadorEncontradoDto
        {
            PrestadorId = vet.Id,
            Tipo = TipoPrestador.VeterinarioAutonomo,
            Nome = vet.Nome,
            Nota = temNotaPublica ? vet.NotaMedia : null,
            NumAvaliacoes = vet.NumAvaliacoes,
            SeloNovo = !temNotaPublica && EhPerfilNovo(vet.PublicadoEm, agora),
            HorariosLivres48h = disponibilidade.GetValueOrDefault(vet.Id),
            ProximoHorario = proximos.TryGetValue(vet.Id, out var proximo) ? proximo : null,
            EspeciesAtendidas = [.. vet.EspeciesAtendidas],
            Especialidades = [.. vet.Especialidades],
            Localizacao = Localizar(vet.Endereco)
        };

        return new Candidato(dto, lat, lng);
    }

    private static Candidato? MontarDaEmpresa(
        Empresa empresa,
        CandidatosDoMatching candidatos,
        Dictionary<Guid, int> disponibilidade,
        Dictionary<Guid, DateTime> proximos,
        DateTime agora)
    {
        if (empresa.Endereco?.Latitude is not { } lat || empresa.Endereco?.Longitude is not { } lng)
            return null;

        var equipe = candidatos.VinculadosPorEmpresa[empresa.Id];

        // A clinica herda da equipe o que a RN-029 e a RN-030 precisam: atende a especie
        // que qualquer um dos seus profissionais atende, e a nota dela e a media
        // ponderada dos que ja tem nota publica (RN-057). Nao ha coluna de reputacao na
        // empresa, e derivar da equipe e mais fiel do que inventar uma.
        var comNota = equipe.Where(v => v.NumAvaliacoes >= MinimoDeAvaliacoes).ToList();

        decimal? nota = comNota.Count == 0
            ? null
            : Math.Round(comNota.Sum(v => v.NotaMedia * v.NumAvaliacoes) / comNota.Sum(v => v.NumAvaliacoes), 2);

        var horariosDaEquipe = equipe
            .Select(v => proximos.TryGetValue(v.Id, out var p) ? p : (DateTime?)null)
            .Where(p => p is not null)
            .Select(p => p!.Value)
            .ToList();

        var publicadaEm = equipe
            .Where(v => v.PublicadoEm is not null)
            .Select(v => v.PublicadoEm!.Value)
            .DefaultIfEmpty()
            .Min();

        var dto = new PrestadorEncontradoDto
        {
            PrestadorId = empresa.Id,
            Tipo = TipoPrestador.Empresa,
            Nome = empresa.Nome,
            Nota = nota,
            NumAvaliacoes = equipe.Sum(v => v.NumAvaliacoes),
            SeloNovo = nota is null && publicadaEm != default && EhPerfilNovo(publicadaEm, agora),
            HorariosLivres48h = equipe.Sum(v => disponibilidade.GetValueOrDefault(v.Id)),
            ProximoHorario = horariosDaEquipe.Count == 0 ? null : horariosDaEquipe.Min(),
            EspeciesAtendidas = [.. equipe.SelectMany(v => v.EspeciesAtendidas).Distinct()],
            Especialidades = [.. equipe.SelectMany(v => v.Especialidades).Distinct()],
            Localizacao = Localizar(empresa.Endereco)
        };

        return new Candidato(dto, lat, lng);
    }

    /// <summary>
    /// Aplica os filtros eliminatórios, calcula distância e score. Devolve
    /// <c>false</c> para o candidato que não entra no resultado.
    /// </summary>
    private static bool Elegivel(
        Candidato candidato, CandidatosDoMatching candidatos, FiltroBuscaDto filtro, Animal animal,
        decimal latitude, decimal longitude, double raioKm, DateTime agora)
    {
        var dto = candidato.Dto;

        // RN-029: especie atendida e filtro ELIMINATORIO — matching clinicamente
        // invalido nao pode aparecer nem no fim da lista.
        if (!dto.EspeciesAtendidas.Any(e => e.Equals(animal.Especie, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (!string.IsNullOrWhiteSpace(filtro.Especialidade) &&
            !dto.Especialidades.Any(e => e.Contains(filtro.Especialidade, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // Necessidade informada: o prestador precisa oferecer o servico (RN-002/RN-032)
        if (filtro.Necessidade is { } necessidade)
        {
            var servicos = candidatos.ServicosPorPrestador.GetValueOrDefault(dto.PrestadorId) ?? [];
            var servico = servicos.FirstOrDefault(s => s.Tipo == necessidade);

            if (servico is null)
                return false;

            dto.ValorServico = servico.Valor;

            if (filtro.ValorMinimo is { } minimo && servico.Valor < minimo) return false;
            if (filtro.ValorMaximo is { } maximo && servico.Valor > maximo) return false;
        }

        // "Atende hoje" (RN-032): so entra quem tem horario livre ainda hoje
        if (filtro.AtendeHoje &&
            (dto.ProximoHorario is null || dto.ProximoHorario.Value.Date != agora.Date))
        {
            return false;
        }

        var distancia = Geo.DistanciaEmKm(latitude, longitude, candidato.Latitude, candidato.Longitude);

        // O bounding box e um retangulo: os cantos ficam fora do raio e sao descartados aqui
        if (distancia > raioKm)
            return false;

        dto.DistanciaKm = Math.Round(distancia, 1);
        CalcularScore(dto, raioKm);
        return true;
    }

    /// <summary>
    /// Score de ordenação (RN-030): distância 40%, avaliação 30%, disponibilidade 30%.
    ///
    /// Prestador sem nota pública é ordenado apenas por distância e disponibilidade
    /// (RN-030/RN-033) — sem boost artificial e sem nota inventada. Os dois pesos
    /// restantes são renormalizados proporcionalmente (57/43), conforme a pendência
    /// P-09; sem isso quem ainda não tem nota competiria com 30% do score zerado, que
    /// é exatamente a punição ao entrante que a RN-033 quer evitar.
    /// </summary>
    private static void CalcularScore(PrestadorEncontradoDto dto, double raioKm)
    {
        // Quanto mais perto, maior: no limite do raio vale 0, na porta vale 1
        var fatorDistancia = Math.Clamp(1 - (dto.DistanciaKm / raioKm), 0, 1);

        var fatorDisponibilidade =
            Math.Clamp((double)Math.Min(dto.HorariosLivres48h, TetoDeHorarios48h) / TetoDeHorarios48h, 0, 1);

        var temNota = dto.Nota is not null;
        var fatorAvaliacao = temNota ? (double)dto.Nota!.Value / 5.0 : 0;

        double pesoDistancia, pesoAvaliacao, pesoDisponibilidade;

        if (temNota)
        {
            pesoDistancia = PesoDistancia;
            pesoAvaliacao = PesoAvaliacao;
            pesoDisponibilidade = PesoDisponibilidade;
        }
        else
        {
            var soma = PesoDistancia + PesoDisponibilidade;
            pesoDistancia = PesoDistancia / soma;              // ~0,571
            pesoAvaliacao = 0;
            pesoDisponibilidade = PesoDisponibilidade / soma;  // ~0,429
        }

        dto.Score = Math.Round(
            (fatorDistancia * pesoDistancia) +
            (fatorAvaliacao * pesoAvaliacao) +
            (fatorDisponibilidade * pesoDisponibilidade), 4);

        dto.Composicao = new ComposicaoDoScoreDto
        {
            Distancia = Math.Round(fatorDistancia, 4),
            Avaliacao = Math.Round(fatorAvaliacao, 4),
            Disponibilidade = Math.Round(fatorDisponibilidade, 4),
            PesoDistancia = Math.Round(pesoDistancia, 4),
            PesoAvaliacao = Math.Round(pesoAvaliacao, 4),
            PesoDisponibilidade = Math.Round(pesoDisponibilidade, 4)
        };
    }

    private static bool EhPerfilNovo(DateTime? publicadoEm, DateTime agora) =>
        publicadoEm is not null && (agora - publicadoEm.Value).TotalDays <= DiasDoSeloNovo;

    private static string Localizar(Endereco? endereco) =>
        endereco is null ? string.Empty : $"{endereco.Bairro}, {endereco.Cidade}/{endereco.Uf}".TrimStart(',', ' ');
}
