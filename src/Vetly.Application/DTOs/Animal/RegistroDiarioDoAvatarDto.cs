using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Animal;

/// <summary>
/// A condição do avatar num dia (RN-114). É o que alimenta o gráfico de evolução do
/// pet no app: saúde e humor ao longo das semanas mostram ao Responsável o efeito de
/// manter as obrigações em dia.
/// </summary>
public class RegistroDiarioDoAvatarDto
{
    /// <summary>Animal do registro.</summary>
    public Guid AnimalId { get; set; }

    /// <summary>Dia do registro (UTC). Um registro por animal por dia.</summary>
    public DateOnly Data { get; set; }

    /// <summary>Saúde de 0 a 100 naquele dia.</summary>
    public int Saude { get; set; }

    /// <summary>Humor de 0 a 100 naquele dia.</summary>
    public int Humor { get; set; }

    /// <summary>Estado visual naquele dia.</summary>
    public EstadoDoAvatar Estado { get; set; }

    /// <summary>Comprimento do pelo, de 0 a 3.</summary>
    public int NivelDoPelo { get; set; }

    /// <summary>Motivos do dia, do mais grave ao mais leve.</summary>
    public List<MotivoDoAvatar> Motivos { get; set; } = [];

    /// <summary>Instante do cálculo gravado (UTC) — o último do dia.</summary>
    public DateTime CalculadoEm { get; set; }

    /// <summary>Monta o registro do dia a partir do avatar calculado.</summary>
    public static RegistroDiarioDoAvatarDto De(AvatarDoPetDto avatar) => new()
    {
        AnimalId = avatar.AnimalId,
        Data = DateOnly.FromDateTime(avatar.CalculadoEm),
        Saude = avatar.Saude,
        Humor = avatar.Humor,
        Estado = avatar.Estado,
        NivelDoPelo = avatar.NivelDoPelo,
        Motivos = [.. avatar.Motivos],
        CalculadoEm = avatar.CalculadoEm
    };
}
