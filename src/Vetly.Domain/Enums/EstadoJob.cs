namespace Vetly.Domain.Enums;

/// <summary>Situação de um trabalho na fila (§11).</summary>
public enum EstadoJob
{
    /// <summary>Aguardando a hora de executar.</summary>
    Pendente = 1,

    /// <summary>Executado com sucesso.</summary>
    Concluido = 2,

    /// <summary>Falhou em todas as tentativas e não será mais tentado.</summary>
    Falhou = 3
}
