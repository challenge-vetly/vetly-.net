using System.Globalization;
using MongoDB.Driver;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.NoSql;

/// <summary>
/// Histórico do avatar no MongoDB (<c>Adaptadores:DocumentoNoSql = Mongo</c>).
///
/// <para>
/// Os índices — único em (<c>animalId</c>, <c>data</c>) e simples em
/// <c>animalId</c> — são criados na primeira operação, uma vez por processo.
/// <c>CreateMany</c> é idempotente no Mongo: índice que já existe com a mesma
/// definição não é recriado. Fica aqui, e não num script à parte, porque o índice
/// único é parte da regra "um registro por dia", não um ajuste de desempenho.
/// </para>
/// </summary>
public sealed class HistoricoDoAvatarRepositoryMongo : IHistoricoDoAvatarRepository
{
    /// <summary>Nome da coleção.</summary>
    public const string Colecao = "historico_avatar";

    private const string FormatoDaData = "yyyy-MM-dd";

    private readonly IMongoCollection<DocumentoDoAvatar> _colecao;
    private readonly Lazy<Task> _indices;

    /// <summary>Cria o repositório sobre o banco configurado.</summary>
    public HistoricoDoAvatarRepositoryMongo(IMongoDatabase banco)
    {
        _colecao = banco.GetCollection<DocumentoDoAvatar>(Colecao);
        _indices = new Lazy<Task>(CriarIndicesAsync, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc/>
    public async Task RegistrarAsync(RegistroDiarioDoAvatarDto registro, CancellationToken cancellationToken = default)
    {
        await _indices.Value;

        var documento = ParaDocumento(registro);
        var filtro = Builders<DocumentoDoAvatar>.Filter.Eq(d => d.AnimalId, documento.AnimalId)
                     & Builders<DocumentoDoAvatar>.Filter.Eq(d => d.Data, documento.Data);

        try
        {
            await _colecao.ReplaceOneAsync(filtro, documento, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Dois upserts do mesmo dia ao mesmo tempo (rotina e GET /avatar): os dois
            // nao acham o documento e tentam inserir; o indice unico recusa o segundo.
            // Na repeticao o documento ja existe e o upsert vira substituicao.
            await _colecao.ReplaceOneAsync(filtro, documento, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<ResultadoPaginado<RegistroDiarioDoAvatarDto>> ObterPaginadoAsync(
        Guid animalId, Paginacao paginacao, CriterioDeOrdenacao ordenacao, CancellationToken cancellationToken = default)
    {
        await _indices.Value;

        var filtro = Builders<DocumentoDoAvatar>.Filter.Eq(d => d.AnimalId, animalId.ToString());
        var total = (int)await _colecao.CountDocumentsAsync(filtro, cancellationToken: cancellationToken);

        var documentos = await _colecao.Find(filtro)
            .Sort(Ordenar(ordenacao))
            .Skip(paginacao.Deslocamento)
            .Limit(paginacao.Tamanho)
            .ToListAsync(cancellationToken);

        return new ResultadoPaginado<RegistroDiarioDoAvatarDto>(documentos.Select(ParaRegistro).ToList(), total, paginacao);
    }

    /// <summary>
    /// Campo da whitelist em ordenação do Mongo, com desempate pela data — que é
    /// única por animal, e por isso torna a ordem total e a paginação estável.
    /// </summary>
    private static SortDefinition<DocumentoDoAvatar> Ordenar(CriterioDeOrdenacao criterio)
    {
        var ordem = Builders<DocumentoDoAvatar>.Sort;

        SortDefinition<DocumentoDoAvatar> Direcao(System.Linq.Expressions.Expression<Func<DocumentoDoAvatar, object>> campo) =>
            criterio.Descendente ? ordem.Descending(campo) : ordem.Ascending(campo);

        return criterio.Campo switch
        {
            CamposDeOrdenacao.HistoricoDoAvatar.Saude => ordem.Combine(Direcao(d => d.Saude), ordem.Descending(d => d.Data)),
            CamposDeOrdenacao.HistoricoDoAvatar.Humor => ordem.Combine(Direcao(d => d.Humor), ordem.Descending(d => d.Data)),
            _ => Direcao(d => d.Data)
        };
    }

    private async Task CriarIndicesAsync()
    {
        var chave = Builders<DocumentoDoAvatar>.IndexKeys;

        await _colecao.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<DocumentoDoAvatar>(
                chave.Ascending(d => d.AnimalId).Ascending(d => d.Data),
                new CreateIndexOptions { Unique = true, Name = "ux_animal_data" }),
            new CreateIndexModel<DocumentoDoAvatar>(
                chave.Ascending(d => d.AnimalId),
                new CreateIndexOptions { Name = "ix_animal" })
        ]);
    }

    public static DocumentoDoAvatar ParaDocumento(RegistroDiarioDoAvatarDto registro) => new()
    {
        AnimalId = registro.AnimalId.ToString(),
        Data = registro.Data.ToString(FormatoDaData, CultureInfo.InvariantCulture),
        Saude = registro.Saude,
        Humor = registro.Humor,
        Estado = registro.Estado.ToString(),
        NivelDoPelo = registro.NivelDoPelo,
        Motivos = registro.Motivos.Select(m => m.ToString()).ToList(),
        CalculadoEm = registro.CalculadoEm
    };

    public static RegistroDiarioDoAvatarDto ParaRegistro(DocumentoDoAvatar documento) => new()
    {
        AnimalId = Guid.Parse(documento.AnimalId),
        Data = DateOnly.ParseExact(documento.Data, FormatoDaData, CultureInfo.InvariantCulture),
        Saude = documento.Saude,
        Humor = documento.Humor,
        Estado = Enum.Parse<EstadoDoAvatar>(documento.Estado),

        // Motivo desconhecido (gravado por uma versao futura) e ignorado, e nao erro
        // de leitura: o historico inteiro nao pode ficar ilegivel por um nome novo
        Motivos = documento.Motivos
            .Select(m => Enum.TryParse<MotivoDoAvatar>(m, out var motivo) ? motivo : (MotivoDoAvatar?)null)
            .OfType<MotivoDoAvatar>()
            .ToList(),
        NivelDoPelo = documento.NivelDoPelo,
        CalculadoEm = DateTime.SpecifyKind(documento.CalculadoEm, DateTimeKind.Utc)
    };
}
