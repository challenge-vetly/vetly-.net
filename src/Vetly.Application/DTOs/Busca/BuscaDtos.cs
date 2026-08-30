using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Busca;

/// <summary>
/// Filtros da busca por prestadores (RN-001, RN-002, RN-026 a RN-033).
/// </summary>
public class FiltroBuscaDto
{
    /// <summary>
    /// Animal que será atendido. A espécie dele é filtro eliminatório: vet que não
    /// atende a espécie nunca aparece (RN-029).
    /// </summary>
    [Required(ErrorMessage = "Informe o animal que será atendido.")]
    public Guid AnimalId { get; set; }

    /// <summary>Necessidade do momento (RN-002/RN-032).</summary>
    public TipoServico? Necessidade { get; set; }

    /// <summary>Latitude do Responsável, do GPS do dispositivo (RN-027).</summary>
    public decimal? Lat { get; set; }

    /// <summary>Longitude do Responsável (RN-027).</summary>
    public decimal? Lng { get; set; }

    /// <summary>
    /// CEP informado pelo Responsável. Fallback quando a permissão de localização é
    /// negada (RN-027).
    /// </summary>
    [MaxLength(9)]
    public string? Cep { get; set; }

    /// <summary>
    /// Raio de busca em quilômetros. Default 10, expansível até 25 pelo
    /// Responsável (RN-028).
    /// </summary>
    [Range(1, 25, ErrorMessage = "O raio deve estar entre 1 e 25 km.")]
    public double? RaioKm { get; set; }

    /// <summary>Filtra por especialidade clínica (RN-032).</summary>
    [MaxLength(100)]
    public string? Especialidade { get; set; }

    /// <summary>Preço mínimo do serviço (RN-032).</summary>
    [Range(0, 999999.99)]
    public decimal? ValorMinimo { get; set; }

    /// <summary>Preço máximo do serviço (RN-032).</summary>
    [Range(0, 999999.99)]
    public decimal? ValorMaximo { get; set; }

    /// <summary>Somente prestadores com horário livre ainda hoje (RN-032).</summary>
    public bool AtendeHoje { get; set; }
}

/// <summary>Tipo do prestador no resultado da busca.</summary>
public enum TipoPrestador
{
    /// <summary>Veterinário autônomo — a consulta é agendada diretamente com ele (RN-003).</summary>
    VeterinarioAutonomo = 1,

    /// <summary>Clínica — a consulta é atribuída ao profissional que ela designar (RN-003).</summary>
    Empresa = 2
}

/// <summary>Origem da posição usada no cálculo de distância (RN-027).</summary>
public enum OrigemDaPosicao
{
    /// <summary>GPS do dispositivo — melhor precisão.</summary>
    Gps = 1,

    /// <summary>CEP informado pelo Responsável, quando a localização foi negada.</summary>
    Cep = 2
}

/// <summary>Um prestador no resultado da busca.</summary>
public class PrestadorEncontradoDto
{
    public Guid PrestadorId { get; set; }

    public TipoPrestador Tipo { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Distância em quilômetros, arredondada em 100 metros.</summary>
    public double DistanciaKm { get; set; }

    /// <summary>
    /// Nota média. Nula enquanto o prestador não tem o mínimo de 3 avaliações —
    /// abaixo disso uma única nota extrema definiria o perfil inteiro (RN-057).
    /// </summary>
    public decimal? Nota { get; set; }

    public int NumAvaliacoes { get; set; }

    /// <summary>
    /// Selo "Novo na Vetly": vale por 30 dias a partir da publicação do perfil e
    /// substitui a nota que o prestador ainda não tem (RN-033).
    /// </summary>
    public bool SeloNovo { get; set; }

    /// <summary>Próximo horário livre, se houver.</summary>
    public DateTime? ProximoHorario { get; set; }

    /// <summary>Horários livres nas próximas 48h — o fator de disponibilidade do score.</summary>
    public int HorariosLivres48h { get; set; }

    /// <summary>Valor do serviço buscado, quando a necessidade foi informada.</summary>
    public decimal? ValorServico { get; set; }

    /// <summary>Espécies atendidas.</summary>
    public List<string> EspeciesAtendidas { get; set; } = [];

    /// <summary>Especialidades.</summary>
    public List<string> Especialidades { get; set; } = [];

    /// <summary>Bairro e cidade, para o app exibir sem repetir o endereço inteiro.</summary>
    public string Localizacao { get; set; } = string.Empty;

    /// <summary>Score final de ordenação, de 0 a 1 (RN-030).</summary>
    public double Score { get; set; }

    /// <summary>Composição do score, para o app poder explicar a ordem ao Responsável.</summary>
    public ComposicaoDoScoreDto Composicao { get; set; } = new();
}

/// <summary>
/// Como o score foi formado (RN-030). Exposto para que a ordenação seja auditável —
/// e para o app conseguir explicar por que um prestador veio antes de outro.
/// </summary>
public class ComposicaoDoScoreDto
{
    /// <summary>Fator de distância, de 0 a 1: quanto mais perto, maior.</summary>
    public double Distancia { get; set; }

    /// <summary>Fator de avaliação, de 0 a 1. Zero quando não há nota pública.</summary>
    public double Avaliacao { get; set; }

    /// <summary>Fator de disponibilidade, de 0 a 1.</summary>
    public double Disponibilidade { get; set; }

    /// <summary>Peso aplicado à distância (40% com nota; renormalizado sem nota).</summary>
    public double PesoDistancia { get; set; }

    /// <summary>Peso aplicado à avaliação (30% com nota; 0 sem nota).</summary>
    public double PesoAvaliacao { get; set; }

    /// <summary>Peso aplicado à disponibilidade (30% com nota; renormalizado sem nota).</summary>
    public double PesoDisponibilidade { get; set; }
}

/// <summary>Resultado da busca, com o contexto do que foi aplicado.</summary>
public class ResultadoBuscaDto
{
    public List<PrestadorEncontradoDto> Itens { get; set; } = [];

    /// <summary>Raio efetivamente aplicado, em quilômetros (RN-028).</summary>
    public double RaioAplicadoKm { get; set; }

    /// <summary>De onde veio a posição usada no cálculo (RN-027).</summary>
    public OrigemDaPosicao Origem { get; set; }

    public int Total { get; set; }
    public int Pagina { get; set; }
    public int Tamanho { get; set; }

    /// <summary>Espécie do animal usada como filtro eliminatório (RN-029).</summary>
    public string EspecieDoAnimal { get; set; } = string.Empty;
}
