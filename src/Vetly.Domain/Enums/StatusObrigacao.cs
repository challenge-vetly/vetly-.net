namespace Vetly.Domain.Enums;

/// <summary>Status persistido de uma <c>ObrigacaoDoPet</c> (RN-069/070). "Atrasada" não é
/// um estado persistido — é derivado comparando <c>DataLimite</c> com o momento atual.</summary>
public enum StatusObrigacao
{
    Pendente = 1,
    Cumprida = 2
}
