using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Animal;

/// <summary>
/// Condição do avatar do pet, calculada no servidor (RN-112 a RN-114).
///
/// Substitui os valores fixos que o app simulava. Tudo aqui vem de
/// <c>CondicaoDoAvatar</c>: o app desenha, não decide.
/// </summary>
public class AvatarDoPetDto
{
    /// <summary>Animal do avatar.</summary>
    public Guid AnimalId { get; set; }

    /// <summary>Saúde de 0 a 100 (RN-112).</summary>
    public int Saude { get; set; }

    /// <summary>Humor de 0 a 100 (RN-112).</summary>
    public int Humor { get; set; }

    /// <summary>Estado visual pela precedência da RN-113.</summary>
    public EstadoDoAvatar Estado { get; set; }

    /// <summary>Comprimento do pelo, de 0 a 3, pelo atraso da higienização (RN-113).</summary>
    public int NivelDoPelo { get; set; }

    /// <summary>O que derrubou saúde ou humor, do mais grave ao mais leve.</summary>
    public List<MotivoDoAvatar> Motivos { get; set; } = [];

    /// <summary>De uma a três frases do pet para o balão do app.</summary>
    public List<string> Falas { get; set; } = [];

    /// <summary>
    /// Há sprite para a espécie. No MVP só cachorro tem (RN-096); para as demais o
    /// app mostra a foto com os indicadores, sem animação.
    /// </summary>
    public bool SpriteDisponivel { get; set; }

    /// <summary>Instante do cálculo (UTC).</summary>
    public DateTime CalculadoEm { get; set; }
}
