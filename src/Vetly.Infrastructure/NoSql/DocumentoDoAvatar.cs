using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Vetly.Infrastructure.NoSql;

/// <summary>
/// Documento da coleção <c>historico_avatar</c> (RN-114).
///
/// <para>
/// Mora na Infrastructure, e não no Domain, porque carrega atributos do driver do
/// Mongo: o Domain continua sem dependência de persistência nenhuma, e a troca de
/// banco de documentos fica restrita a este arquivo e ao adaptador.
/// </para>
/// <para>
/// Formato: <c>{ _id, animalId, data: "yyyy-MM-dd", saude, humor, estado,
/// nivelDoPelo, motivos: [], calculadoEm }</c>. O id do animal e o estado vão como
/// texto: legíveis numa consulta manual no shell e livres da configuração de
/// representação de GUID do driver, que muda entre versões.
/// </para>
/// </summary>
public sealed class DocumentoDoAvatar
{
    /// <summary>
    /// Gerado pelo servidor no primeiro upsert e omitido nos seguintes — enviar um id
    /// vazio na substituição tentaria trocar o <c>_id</c>, que o Mongo recusa.
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfDefault]
    public string? Id { get; set; }

    /// <summary>Animal, como texto no formato padrão de GUID.</summary>
    [BsonElement("animalId")]
    public string AnimalId { get; set; } = string.Empty;

    /// <summary>Dia no formato <c>yyyy-MM-dd</c>: ordena como texto na mesma ordem das datas.</summary>
    [BsonElement("data")]
    public string Data { get; set; } = string.Empty;

    /// <summary>Saúde de 0 a 100.</summary>
    [BsonElement("saude")]
    public int Saude { get; set; }

    /// <summary>Humor de 0 a 100.</summary>
    [BsonElement("humor")]
    public int Humor { get; set; }

    /// <summary>Nome do estado (<c>EstadoDoAvatar</c>).</summary>
    [BsonElement("estado")]
    public string Estado { get; set; } = string.Empty;

    /// <summary>Comprimento do pelo, de 0 a 3.</summary>
    [BsonElement("nivelDoPelo")]
    public int NivelDoPelo { get; set; }

    /// <summary>Nomes dos motivos (<c>MotivoDoAvatar</c>).</summary>
    [BsonElement("motivos")]
    public List<string> Motivos { get; set; } = [];

    /// <summary>Instante do cálculo (UTC).</summary>
    [BsonElement("calculadoEm")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CalculadoEm { get; set; }
}
